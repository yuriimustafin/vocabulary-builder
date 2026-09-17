import React, { Component } from 'react';
import { Button, Form, FormGroup, Label, Input, Alert, Card, CardBody, CardHeader, Spinner } from 'reactstrap';
import { ImportResultSummary } from './ImportResultSummary';

const PLACEHOLDER = `un point de vue=viewpoint
un extraterrestre
bien-sûr=of course
un dessin-animé=anime
un pas=step
migrer=to migrate
une oeuvre d'art=masterpiece
également=also
mettre en scène=to stage
similaire`;

export class NotesImport extends Component {
  static displayName = NotesImport.name;

  constructor(props) {
    super(props);
    this.state = {
      notes: '',
      listName: '',
      loading: false,
      result: null,
      error: null
    };
  }

  handleInputChange = (e) => {
    this.setState({ [e.target.name]: e.target.value });
  }

  handleSubmit = async (e) => {
    e.preventDefault();

    const { notes, listName } = this.state;

    if (!notes.trim()) {
      this.setState({ error: 'Please paste your lesson notes' });
      return;
    }

    this.setState({ loading: true, error: null, result: null });

    try {
      const lang = localStorage.getItem('language') || 'fr';
      const params = new URLSearchParams({ lang });
      if (listName) {
        params.append('listName', listName);
      }

      const response = await fetch(`/api/NewWords/import-notes?${params.toString()}`, {
        method: 'POST',
        headers: { 'Content-Type': 'text/plain' },
        body: notes
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      this.setState({
        loading: false,
        result: await response.json(),
        notes: '',
        error: null
      });
    } catch (error) {
      console.error('Error importing lesson notes:', error);
      this.setState({
        loading: false,
        error: `Error importing lesson notes: ${error.message || 'Please try again.'}`
      });
    }
  }

  handleClear = () => {
    this.setState({ notes: '', listName: '', result: null, error: null });
  }

  render() {
    const { notes, listName, loading, result, error } = this.state;

    return (
      <div>
        <h1>Import from Lesson Notes</h1>
        <p className="lead">
          Paste the notes from a lesson and the vocabulary in them will be picked out for you.
        </p>

        <Card className="mb-4">
          <CardHeader>
            <h5 className="mb-0">What this import does</h5>
          </CardHeader>
          <CardBody>
            <ul className="mb-0">
              <li>Paste the notes as they are — no need to tidy them up first</li>
              <li>
                Items written against a translation ("un pas=step") are read, and the
                translation is discarded
              </li>
              <li>
                Several items on one line are told apart: "un chien, un chat" is two words,
                while "une pomme de terre" stays one
              </li>
              <li>
                Terms are stored under their <strong>headword</strong>, so a word you already
                have gains an encounter instead of a duplicate
              </li>
              <li>
                Definitions come from the <strong>dictionary</strong>, not from your notes,
                and are fetched when the words are exported
              </li>
              <li>Headings, dates and anything that is not vocabulary are listed back to you</li>
            </ul>
          </CardBody>
        </Card>

        <Form onSubmit={this.handleSubmit}>
          <FormGroup>
            <Label for="listName">Lesson Name (Optional)</Label>
            <Input
              type="text"
              name="listName"
              id="listName"
              value={listName}
              onChange={this.handleInputChange}
              placeholder="e.g., Lesson 12, Preply 3 March"
              disabled={loading}
            />
            <small className="form-text text-muted">
              Naming the lesson keeps a second paste of the same notes from counting twice
            </small>
          </FormGroup>

          <FormGroup>
            <Label for="notes">Lesson Notes *</Label>
            <Input
              type="textarea"
              name="notes"
              id="notes"
              value={notes}
              onChange={this.handleInputChange}
              rows="14"
              placeholder={PLACEHOLDER}
              required
              disabled={loading}
              style={{ fontFamily: 'monospace' }}
            />
            <small className="form-text text-muted">
              {notes.split('\n').filter(line => line.trim()).length} lines entered
            </small>
          </FormGroup>

          <div className="d-flex gap-2">
            <Button color="primary" type="submit" disabled={loading || !notes.trim()}>
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
            Reading the notes and working out the headwords...
          </Alert>
        )}

        <ImportResultSummary result={result} />
      </div>
    );
  }
}
