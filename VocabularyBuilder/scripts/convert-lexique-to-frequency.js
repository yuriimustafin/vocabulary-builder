/*
 * Converts the Lexique 3.83 French lexical database into the frequency-words
 * format consumed by ImportFrequencyWordsCommand:
 *
 *     lemma/frequency -> form1,form2,form3
 *     lemma/frequency
 *
 * Usage:
 *     node scripts/convert-lexique-to-frequency.js <Lexique383.tsv> [output.txt]
 *
 * Getting Lexique383.tsv: lexique.org lapsed and the domain is now parked, so
 * the last published archive has to come from the Wayback Machine:
 *     http://web.archive.org/web/20240330115437id_/http://www.lexique.org/databases/Lexique383/Lexique383.zip
 * Unzip it and point this script at Lexique383.tsv.
 *
 * Lexique 3.83 is CC BY-SA 4.0 (New, Pallier, Brysbaert & Ferrand), so the
 * generated word list carries the same licence - see frequency-words-fr.LICENCE.txt.
 */

const fs = require('fs');
const readline = require('readline');

// Lexique reports frequencies as occurrences per million. The English list uses
// raw corpus counts, so scale up to land in the same order of magnitude.
const FREQUENCY_SCALE = 100;

// Both corpora are available; films (subtitles) matches the English list, which
// carries subtitle artefacts like "goin'" and "'ve".
const LEMMA_FREQUENCY_COLUMN = 'freqlemfilms2';
const FORM_FREQUENCY_COLUMN = 'freqfilms2';

// ~9300 lemmas never appear in the subtitle corpus, and every one of them does
// appear in the book corpus - including words a reader meets constantly, like
// "c'est-a-dire" and "vis-a-vis". Fall back rather than write them off as zero.
// Only zeros fall back, so the ranking stays subtitle-based throughout.
const LEMMA_FALLBACK_FREQUENCY_COLUMN = 'freqlemlivres';
const FORM_FALLBACK_FREQUENCY_COLUMN = 'freqlivres';

async function main() {
    const [inputPath, outputPath = 'frequency-words-fr.txt'] = process.argv.slice(2);

    if (!inputPath) {
        console.error('Usage: node convert-lexique-to-frequency.js <Lexique383.tsv> [output.txt]');
        process.exit(1);
    }

    const { lemmaFrequencies, formLemmaWeights } = await readLexique(inputPath);
    inheritFrequencies(lemmaFrequencies, formLemmaWeights);
    const lemmaForms = assignFormsToLemmas(lemmaFrequencies, formLemmaWeights);
    const lineCount = writeOutput(outputPath, lemmaFrequencies, lemmaForms);

    console.log(`Wrote ${lineCount} lemmas to ${outputPath}`);
}

async function readLexique(inputPath) {
    const stream = readline.createInterface({
        input: fs.createReadStream(inputPath, 'utf8'),
        crlfDelay: Infinity
    });

    /** lemma -> its own frequency (homographs differ by cgram, keep the highest) */
    const lemmaFrequencies = new Map();
    /**
     * surface form -> lemma -> summed frequency of that form in that reading.
     * Summing matters: "suis" splits across etre/AUX and etre/VER, and either
     * row alone loses to suivre even though the combined form is far commoner.
     */
    const formLemmaWeights = new Map();

    let columns = null;

    for await (const line of stream) {
        if (!line) continue;

        const parts = line.split('\t');

        if (columns === null) {
            columns = {
                ortho: parts.indexOf('ortho'),
                lemme: parts.indexOf('lemme'),
                lemmaFrequency: parts.indexOf(LEMMA_FREQUENCY_COLUMN),
                formFrequency: parts.indexOf(FORM_FREQUENCY_COLUMN),
                lemmaFallback: parts.indexOf(LEMMA_FALLBACK_FREQUENCY_COLUMN),
                formFallback: parts.indexOf(FORM_FALLBACK_FREQUENCY_COLUMN)
            };
            if (Object.values(columns).some(i => i < 0)) {
                throw new Error('Unexpected header - Lexique383.tsv should carry ortho, lemme, '
                    + `${LEMMA_FREQUENCY_COLUMN}, ${FORM_FREQUENCY_COLUMN}, `
                    + `${LEMMA_FALLBACK_FREQUENCY_COLUMN} and ${FORM_FALLBACK_FREQUENCY_COLUMN}`);
            }
            continue;
        }

        const form = normalise(parts[columns.ortho]);
        const lemma = normalise(parts[columns.lemme]);
        if (!form || !lemma) continue;

        const lemmaFrequency = scale(parts[columns.lemmaFrequency])
            || scale(parts[columns.lemmaFallback]);
        if (lemmaFrequency === null) continue;

        const known = lemmaFrequencies.get(lemma);
        if (known === undefined || lemmaFrequency > known) {
            lemmaFrequencies.set(lemma, lemmaFrequency);
        }

        const formFrequency = scale(parts[columns.formFrequency])
            || scale(parts[columns.formFallback])
            || 0;

        let weights = formLemmaWeights.get(form);
        if (!weights) {
            weights = new Map();
            formLemmaWeights.set(form, weights);
        }
        weights.set(lemma, (weights.get(lemma) || 0) + formFrequency);
    }

    return { lemmaFrequencies, formLemmaWeights };
}

/*
 * A string that is both a lemma and an inflected form of something else - "est"
 * (east / etre), "pris" (taken / prendre), "ete" (summer / etre) - can only
 * appear once, as a lemma, so it never reaches its verb through BaseForm. Give
 * it the highest frequency of any reading instead, matching GetWordFrequency,
 * which already answers "how common is this string" by taking the maximum.
 */
function inheritFrequencies(lemmaFrequencies, formLemmaWeights) {
    for (const [lemma, frequency] of lemmaFrequencies) {
        const weights = formLemmaWeights.get(lemma);
        if (!weights) continue;

        let highest = frequency;
        for (const source of weights.keys()) {
            const candidate = lemmaFrequencies.get(source);
            if (candidate !== undefined && candidate > highest) {
                highest = candidate;
            }
        }

        if (highest !== frequency) {
            lemmaFrequencies.set(lemma, highest);
        }
    }
}

/*
 * FrequencyWord has a unique index on (Headword, Language), so every headword
 * may appear in the file exactly once. French breaks this constantly: "suis"
 * belongs to both etre and suivre, "pris" to both prendre and the adjective
 * pris. Give each ambiguous form to its most frequent lemma, and never emit a
 * form as a derived form when it is already a lemma in its own right.
 */
function assignFormsToLemmas(lemmaFrequencies, formLemmaWeights) {
    const lemmaForms = new Map();

    for (const [form, weights] of formLemmaWeights) {
        if (lemmaFrequencies.has(form)) continue; // emitted as its own lemma

        const owner = pickOwner(weights, lemmaFrequencies);
        if (owner === null) continue;

        let forms = lemmaForms.get(owner);
        if (!forms) {
            forms = [];
            lemmaForms.set(owner, forms);
        }
        forms.push(form);
    }

    return lemmaForms;
}

/*
 * Pick the lemma this form most often belongs to. Frequency of the form itself
 * beats frequency of the lemma: "tue" is far commoner as a form of "tuer" than
 * of the participle "tu", even though the pronoun "tu" dwarfs both as a lemma.
 */
function pickOwner(weights, lemmaFrequencies) {
    let owner = null;
    let ownerWeight = -Infinity;

    for (const [lemma, weight] of weights) {
        if (!lemmaFrequencies.has(lemma)) continue;

        // Ties broken alphabetically so the output is reproducible.
        if (weight > ownerWeight || (weight === ownerWeight && lemma < owner)) {
            owner = lemma;
            ownerWeight = weight;
        }
    }

    return owner;
}

function writeOutput(outputPath, lemmaFrequencies, lemmaForms) {
    const lemmas = [...lemmaFrequencies.keys()].sort((a, b) => {
        const difference = lemmaFrequencies.get(b) - lemmaFrequencies.get(a);
        return difference !== 0 ? difference : a.localeCompare(b, 'fr');
    });

    const output = fs.createWriteStream(outputPath, 'utf8');

    for (const lemma of lemmas) {
        const forms = lemmaForms.get(lemma);
        const derived = forms && forms.length > 0
            ? ' -> ' + forms.sort((a, b) => a.localeCompare(b, 'fr')).join(',')
            : '';
        output.write(`${lemma}/${lemmaFrequencies.get(lemma)}${derived}\n`);
    }

    output.end();
    return lemmas.length;
}

function scale(value) {
    const frequency = Math.round(parseFloat(value) * FREQUENCY_SCALE);
    return Number.isFinite(frequency) ? frequency : null;
}

/*
 * The file format uses "/", "," and " -> " as delimiters, so a form containing
 * any of them cannot round-trip. Lexique has a handful of such entries.
 */
function normalise(value) {
    const trimmed = (value || '').trim();
    if (!trimmed || trimmed.includes(',') || trimmed.includes('/') || trimmed.includes('->')) {
        return '';
    }
    return trimmed;
}

main().catch(error => {
    console.error(error);
    process.exit(1);
});
