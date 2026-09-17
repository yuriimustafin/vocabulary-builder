import React, { Component } from 'react';
import { Button, Form, FormGroup, Label, Input, Alert, Card, CardBody, CardHeader, Spinner } from 'reactstrap';
import { ImportResultSummary } from './ImportResultSummary';

export class LingQImport extends Component {
  static displayName = LingQImport.name;

  constructor(props) {
    super(props);
    this.state = {
      selectedFile: null,
      listName: '',
      loading: false,
      result: null,
      error: null
    };
  }

  handleFileChange = (e) => {
    this.setState({ selectedFile: e.target.files[0], error: null });
  }

  handleInputChange = (e) => {
    this.setState({ [e.target.name]: e.target.value });
  }

  handleSubmit = async (e) => {
    e.preventDefault();

    const { selectedFile, listName } = this.state;

    if (!selectedFile) {
      this.setState({ error: 'Please choose your LingQ export file' });
      return;
    }

    this.setState({ loading: true, error: null, result: null });

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);

      const lang = localStorage.getItem('language') || 'fr';
      const params = new URLSearchParams({ lang });
      if (listName) {
        params.append('listName', listName);
      }

      const response = await fetch(`/api/NewWords/import-lingq?${params.toString()}`, {
        method: 'POST',
        body: formData
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      this.setState({
        loading: false,
        result: await response.json(),
        selectedFile: null,
        error: null
      });

      document.getElementById('lingqFileInput').value = '';
    } catch (error) {
      console.error('Error importing LingQ vocabulary:', error);
      this.setState({
        loading: false,
        error: `Error importing LingQ vocabulary: ${error.message || 'Please try again.'}`
      });
    }
  }

  handleClear = () => {
    this.setState({ selectedFile: null, listName: '', result: null, error: null });
    document.getElementById('lingqFileInput').value = '';
  }

  render() {
    const { selectedFile, listName, loading, result, error } = this.state;

    return (
      <div>
        <h1>Import from LingQ</h1>
        <p className="lead">
          Import your saved vocabulary from a LingQ CSV export.
        </p>

        <Card className="mb-4">
          <CardHeader>
            <h5 className="mb-0">What this import does</h5>
          </CardHeader>
          <CardBody>
            <ul className="mb-0">
              <li>Export your vocabulary from LingQ as CSV and choose the file below</li>
              <li>
                Terms are stored under their <strong>headword</strong>: "une randonnée" and
                "la randonnée" become one word met twice, not two words
              </li>
              <li>Conjugated verbs are traced back to their infinitive — "vous allez" counts towards "aller"</li>
              <li>
                Definitions come from the <strong>dictionary</strong>, not from your LingQ
                translations, and are fetched when the words are exported
              </li>
              <li>Sentences and set phrases are listed back to you rather than imported</li>
              <li>Re-importing the same export adds nothing; a later, longer export adds only its new terms</li>
            </ul>
          </CardBody>
        </Card>

        <Form onSubmit={this.handleSubmit}>
          <FormGroup>
            <Label for="listName">Import Name (Optional)</Label>
            <Input
              type="text"
              name="listName"
              id="listName"
              value={listName}
              onChange={this.handleInputChange}
              placeholder="e.g., Preply, LingQ March"
              disabled={loading}
            />
            <small className="form-text text-muted">
              Naming the import lets a later, longer export add only its new terms
            </small>
          </FormGroup>

          <FormGroup>
            <Label for="lingqFileInput">LingQ CSV Export *</Label>
            <Input
              type="file"
              name="lingqFileInput"
              id="lingqFileInput"
              onChange={this.handleFileChange}
              accept=".csv,text/csv"
              disabled={loading}
              required
            />
            <small className="form-text text-muted">
              {selectedFile
                ? `Selected: ${selectedFile.name} (${(selectedFile.size / 1024).toFixed(2)} KB)`
                : 'No file selected'}
            </small>
          </FormGroup>

          <div className="d-flex gap-2">
            <Button color="primary" type="submit" disabled={loading || !selectedFile}>
              {loading ? (
                <>
                  <Spinner size="sm" className="me-2" />
                  Importing...
                </>
              ) : (
                'Import Vocabulary'
              )}
            </Button>
            <Button color="secondary" type="button" onClick={this.handleClear} disabled={loading}>
              Clear
            </Button>
          </div>
        </Form>

        {error && (
          <Alert color="danger" className="mt-3">
            <strong>Error:</strong> {error}
          </Alert>
        )}

        {loading && (
          <Alert color="info" className="mt-3">
            <Spinner size="sm" className="me-2" />
            Reading the export and working out the headwords...
          </Alert>
        )}

        <ImportResultSummary result={result} />
      </div>
    );
  }
}
