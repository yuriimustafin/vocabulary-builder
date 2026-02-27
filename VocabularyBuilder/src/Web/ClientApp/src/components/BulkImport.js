import React, { Component } from 'react';
import { Button, Form, FormGroup, Label, Input, Alert, Card, CardBody, CardHeader, Spinner } from 'reactstrap';
import { NewWordsClient } from '../web-api-client.ts';

export class BulkImport extends Component {
  static displayName = BulkImport.name;

  constructor(props) {
    super(props);
    this.state = {
      wordList: '',
      listName: '',
      loading: false,
      result: null,
      error: null
    };
  }

  handleInputChange = (e) => {
    const { name, value } = e.target;
    this.setState({ [name]: value });
  }

  handleSubmit = async (e) => {
    e.preventDefault();
    
    const { wordList, listName } = this.state;
    
    if (!wordList.trim()) {
      this.setState({ error: 'Please enter at least one word or URL' });
      return;
    }

    this.setState({ loading: true, error: null, result: null });

    try {
      const client = new NewWordsClient();
      const lang = localStorage.getItem('language') || 'en';
      
      // Create a request with body content
      const response = await fetch('/api/NewWords/import' + 
        (listName ? `?listName=${encodeURIComponent(listName)}&lang=${lang}` : `?lang=${lang}`), {
        method: 'POST',
        headers: {
          'Content-Type': 'text/plain'
        },
        body: wordList
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const result = await response.text();
      
      this.setState({ 
        loading: false, 
        result: result,
        wordList: '', // Clear the textarea after successful import
        error: null
      });
    } catch (error) {
      console.error('Error importing words:', error);
      this.setState({ 
        loading: false, 
        error: `Error importing words: ${error.message}` 
      });
    }
  }

  handleClear = () => {
    this.setState({
      wordList: '',
      listName: '',
      result: null,
      error: null
    });
  }

  render() {
    const { wordList, listName, loading, result, error } = this.state;

    return (
      <div>
        <h1>Bulk Word Import</h1>
        <p className="lead">
          Import multiple words from Oxford Dictionary. You can enter either word lists or Oxford Dictionary URLs.
        </p>

        <Card className="mb-4">
          <CardHeader>
            <h5 className="mb-0">Import Instructions</h5>
          </CardHeader>
          <CardBody>
            <ul className="mb-0">
              <li>Enter <strong>one word per line</strong> or paste Oxford Dictionary URLs</li>
              <li>Words will be fetched from Oxford Dictionary and saved to your vocabulary</li>
              <li><strong>List Name</strong> (optional): Provide a name for idempotency - re-importing the same named list won't create duplicates</li>
              <li>Without a list name, the system uses a hash of the content - same exact list won't create duplicates</li>
            </ul>
          </CardBody>
        </Card>

        <Form onSubmit={this.handleSubmit}>
          <FormGroup>
            <Label for="listName">List Name (Optional)</Label>
            <Input
              type="text"
              name="listName"
              id="listName"
              value={listName}
              onChange={this.handleInputChange}
              placeholder="e.g., Chapter 5, IELTS Week 1, Book: Catching Fire"
              disabled={loading}
            />
            <small className="form-text text-muted">
              Naming your list prevents duplicates when re-importing the same list
            </small>
          </FormGroup>

          <FormGroup>
            <Label for="wordList">Word List *</Label>
            <Input
              type="textarea"
              name="wordList"
              id="wordList"
              value={wordList}
              onChange={this.handleInputChange}
              rows="12"
              placeholder={'Enter words or URLs (one per line):\n\npersist\nleached\nsnare\n\nOr paste Oxford URLs:\nhttps://www.oxfordlearnersdictionaries.com/definition/english/persist\nhttps://www.oxfordlearnersdictionaries.com/definition/english/leach'}
              required
              disabled={loading}
              style={{ fontFamily: 'monospace' }}
            />
            <small className="form-text text-muted">
              {wordList.split('\n').filter(line => line.trim()).length} lines entered
            </small>
          </FormGroup>

          <div className="d-flex gap-2">
            <Button 
              color="primary" 
              type="submit" 
              disabled={loading || !wordList.trim()}
            >
              {loading ? (
                <>
                  <Spinner size="sm" className="me-2" />
                  Importing...
                </>
              ) : (
                'Import Words'
              )}
            </Button>
            <Button 
              color="secondary" 
              type="button" 
              onClick={this.handleClear}
              disabled={loading}
            >
              Clear
            </Button>
          </div>
        </Form>

        {error && (
          <Alert color="danger" className="mt-3">
            <strong>Error:</strong> {error}
          </Alert>
        )}

        {result && (
          <Alert color="success" className="mt-3">
            <h5 className="alert-heading">Import Successful!</h5>
            <hr />
            <div style={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '0.9rem' }}>
              {result}
            </div>
          </Alert>
        )}

        {loading && (
          <Alert color="info" className="mt-3">
            <Spinner size="sm" className="me-2" />
            Fetching word definitions from Oxford Dictionary... This may take a moment.
          </Alert>
        )}
      </div>
    );
  }
}
