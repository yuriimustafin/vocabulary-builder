/**
 * Mock Data Generator for E2E Tests
 * 
 * This script calls the real Oxford Dictionary API and GPT API to capture
 * real responses and save them as mock data files for testing.
 * 
 * Usage: node tests/e2e/mocks/generate-mock-data.js
 */

const https = require('https');
const fs = require('fs');
const path = require('path');

// Configuration
const MOCK_DATA_DIR = path.join(__dirname, '..', '..', '..', 'src', 'Web', 'MockData');
const OXFORD_MOCK_DIR = path.join(MOCK_DATA_DIR, 'oxford');
const GPT_MOCK_DIR = path.join(MOCK_DATA_DIR, 'gpt');

// Ensure directories exist
[MOCK_DATA_DIR, OXFORD_MOCK_DIR, GPT_MOCK_DIR].forEach(dir => {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
});

// List of words to fetch from Oxford Dictionary
const OXFORD_WORDS = [
  'test',
  'example',
  'vocabulary',
  'eloquent',
  'benevolent',
  'ephemeral',
  'ubiquitous',
  'paradigm',
  'serendipity',
  'ambiguous'
];

// List of French words to fetch from GPT
const FRENCH_WORDS = [
  'bonjour',
  'merci',
  'parler',
  'maison',
  'livre',
  'écrire',
  'comprendre',
  'aujourd\'hui',
  'demain',
  'vouloir'
];

/**
 * Fetch a page from Oxford Learner's Dictionaries
 */
function fetchOxfordWord(word) {
  return new Promise((resolve, reject) => {
    const url = `https://www.oxfordlearnersdictionaries.com/us/definition/english/${word}_1`;
    
    console.log(`Fetching Oxford: ${word}...`);
    
    https.get(url, (res) => {
      let html = '';
      
      res.on('data', (chunk) => {
        html += chunk;
      });
      
      res.on('end', () => {
        if (res.statusCode === 200) {
          const filename = path.join(OXFORD_MOCK_DIR, `${word}.html`);
          fs.writeFileSync(filename, html);
          console.log(`✓ Saved: ${filename}`);
          resolve({ word, success: true });
        } else {
          console.log(`✗ Failed to fetch ${word}: HTTP ${res.statusCode}`);
          resolve({ word, success: false, status: res.statusCode });
        }
      });
    }).on('error', (err) => {
      console.error(`✗ Error fetching ${word}:`, err.message);
      reject(err);
    });
  });
}

/**
 * Generate mock GPT response for French word
 * Since we don't have a real API key in the test environment,
 * we'll create realistic mock responses manually
 */
function generateGptMockResponse(word) {
  const mockResponses = {
    'bonjour': {
      word: 'bonjour',
      partOfSpeech: 'interjection',
      translation: 'hello, good morning, good afternoon',
      definition: 'A greeting used during the day',
      examples: [
        {
          french: 'Bonjour, comment allez-vous?',
          english: 'Hello, how are you?'
        },
        {
          french: 'Bonjour à tous!',
          english: 'Hello everyone!'
        }
      ]
    },
    'merci': {
      word: 'merci',
      partOfSpeech: 'interjection',
      translation: 'thank you, thanks',
      definition: 'Expression of gratitude',
      examples: [
        {
          french: 'Merci beaucoup!',
          english: 'Thank you very much!'
        },
        {
          french: 'Merci pour votre aide.',
          english: 'Thank you for your help.'
        }
      ]
    },
    'parler': {
      word: 'parler',
      partOfSpeech: 'verb',
      translation: 'to speak, to talk',
      definition: 'To communicate using words',
      examples: [
        {
          french: 'Je parle français.',
          english: 'I speak French.'
        },
        {
          french: 'Nous parlons de la situation.',
          english: 'We are talking about the situation.'
        }
      ]
    },
    'maison': {
      word: 'maison',
      partOfSpeech: 'noun',
      translation: 'house, home',
      definition: 'A building for living in',
      examples: [
        {
          french: 'Ma maison est grande.',
          english: 'My house is big.'
        },
        {
          french: 'Rentrons à la maison.',
          english: 'Let\'s go home.'
        }
      ]
    },
    'livre': {
      word: 'livre',
      partOfSpeech: 'noun',
      translation: 'book',
      definition: 'A written or printed work consisting of pages',
      examples: [
        {
          french: 'Je lis un livre intéressant.',
          english: 'I am reading an interesting book.'
        },
        {
          french: 'Ce livre est très populaire.',
          english: 'This book is very popular.'
        }
      ]
    },
    'écrire': {
      word: 'écrire',
      partOfSpeech: 'verb',
      translation: 'to write',
      definition: 'To mark letters, words, or symbols on a surface',
      examples: [
        {
          french: 'J\'écris une lettre.',
          english: 'I am writing a letter.'
        },
        {
          french: 'Elle écrit un roman.',
          english: 'She is writing a novel.'
        }
      ]
    },
    'comprendre': {
      word: 'comprendre',
      partOfSpeech: 'verb',
      translation: 'to understand, to comprehend',
      definition: 'To grasp the meaning of something',
      examples: [
        {
          french: 'Je comprends ce que vous dites.',
          english: 'I understand what you are saying.'
        },
        {
          french: 'C\'est difficile à comprendre.',
          english: 'It is difficult to understand.'
        }
      ]
    },
    'aujourd\'hui': {
      word: 'aujourd\'hui',
      partOfSpeech: 'adverb',
      translation: 'today',
      definition: 'On or during this present day',
      examples: [
        {
          french: 'Aujourd\'hui, il fait beau.',
          english: 'Today, the weather is nice.'
        },
        {
          french: 'Que faites-vous aujourd\'hui?',
          english: 'What are you doing today?'
        }
      ]
    },
    'demain': {
      word: 'demain',
      partOfSpeech: 'adverb',
      translation: 'tomorrow',
      definition: 'On the day after today',
      examples: [
        {
          french: 'À demain!',
          english: 'See you tomorrow!'
        },
        {
          french: 'Demain, nous partons en voyage.',
          english: 'Tomorrow, we are going on a trip.'
        }
      ]
    },
    'vouloir': {
      word: 'vouloir',
      partOfSpeech: 'verb',
      translation: 'to want, to wish',
      definition: 'To desire or wish for something',
      examples: [
        {
          french: 'Je veux apprendre le français.',
          english: 'I want to learn French.'
        },
        {
          french: 'Voulez-vous du café?',
          english: 'Would you like some coffee?'
        }
      ]
    }
  };
  
  const response = mockResponses[word] || {
    word: word,
    partOfSpeech: 'noun',
    translation: `translation of ${word}`,
    definition: `Definition of ${word}`,
    examples: [
      {
        french: `Example sentence with ${word}.`,
        english: `Example sentence with ${word} translated.`
      }
    ]
  };
  
  const prompt = `You are a French-English dictionary assistant. For each French word provided, return a JSON response with linguistic information.\n\nProvide dictionary information for the French word: "${word}"`;
  
  const mockData = {
    Prompt: prompt,
    Response: JSON.stringify(response, null, 2),
    CreatedAt: new Date().toISOString()
  };
  
  const filename = path.join(GPT_MOCK_DIR, `${word}.json`);
  fs.writeFileSync(filename, JSON.stringify(mockData, null, 2));
  console.log(`✓ Generated GPT mock: ${filename}`);
  
  return { word, success: true };
}

/**
 * Main execution
 */
async function main() {
  console.log('=== Mock Data Generator ===\n');
  
  console.log('Generating Oxford Dictionary mock data...\n');
  
  // Fetch Oxford words sequentially to avoid rate limiting
  for (const word of OXFORD_WORDS) {
    try {
      await fetchOxfordWord(word);
      // Add delay to be respectful to the API
      await new Promise(resolve => setTimeout(resolve, 1000));
    } catch (error) {
      console.error(`Failed to fetch ${word}:`, error.message);
    }
  }
  
  console.log('\nGenerating GPT mock data...\n');
  
  // Generate GPT mocks
  FRENCH_WORDS.forEach(word => {
    try {
      generateGptMockResponse(word);
    } catch (error) {
      console.error(`Failed to generate mock for ${word}:`, error.message);
    }
  });
  
  console.log('\n=== Mock data generation complete! ===\n');
  console.log(`Oxford mocks: ${OXFORD_MOCK_DIR}`);
  console.log(`GPT mocks: ${GPT_MOCK_DIR}`);
  console.log('\nNote: To use these mocks, run tests with the E2E launch profile.');
}

// Run if called directly
if (require.main === module) {
  main().catch(console.error);
}

module.exports = { fetchOxfordWord, generateGptMockResponse };
