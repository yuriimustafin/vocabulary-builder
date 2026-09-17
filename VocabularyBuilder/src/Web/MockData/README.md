# Mock data directories

These directories contain pre-recorded API responses for testing:

- **oxford/**: HTML files from Oxford Learner's Dictionaries
- **gpt/**: JSON files with GPT API responses for French words
- **wordreference/**: HTML pages from WordReference, used for French

Mock data is generated using the `generate-mock-data.js` script.

## wordreference/

Trimmed recordings of wordreference.com, enabled by `WordReference:UseMockMode`.
File names follow the URL:

| URL | File |
| --- | --- |
| `/fren/prendre` | `prendre.html` |
| `/conj/frverbs.aspx?v=prendre` | `prendre.conj.html` |

Each entry page keeps only the pronunciation span and the Principal Translations
table; each conjugation page keeps the mood headings and their tables. The rest
of the page is layout and is dropped, so the files stay small.

A word with no file here is treated as absent from the dictionary, which is what
makes the fallback to GPT testable.

Note that `.html` files need the explicit `Content Include` rule in `Web.csproj`
to reach the build output - unlike `.json`, the Web SDK does not copy them.
