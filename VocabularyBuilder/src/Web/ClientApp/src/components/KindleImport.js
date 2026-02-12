import React, { Component } from 'react';
import { Button, Form, FormGroup, Label, Input, Alert, Card, CardBody, CardHeader, Spinner } from 'reactstrap';

export class KindleImport extends Component {
  static displayName = KindleImport.name;

  constructor(props) {
    super(props);
    this.state = {
      selectedFile: null,
      loading: false,
      result: null,
      error: null
    };
  }

  handleFileChange = (e) => {
    const file = e.target.files[0];
    this.setState({ 
      selectedFile: file,
      error: null
    });
  }

  handleSubmit = async (e) => {
    e.preventDefault();
    
    const { selectedFile } = this.state;
    
    if (!selectedFile) {
      this.setState({ error: 'Please select a file to upload' });
      return;
    }

    this.setState({ loading: true, error: null, result: null });

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);

      const response = await fetch('/api/NewWords/import-kindle', {
        method: 'POST',
        body: formData
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const result = await response.json();
      
      this.setState({ 
        loading: false, 
        result: `Successfully imported ${result} words from Kindle notes.`,
        selectedFile: null,
        error: null
      });
      
      // Reset the file input
      document.getElementById('fileInput').value = '';
    } catch (error) {
      console.error('Error importing Kindle notes:', error);
      this.setState({ 
        loading: false, 
        error: `Error importing Kindle notes: ${error.message || 'Please try again.'}` 
      });
    }
  }

  handleClear = () => {
    this.setState({
      selectedFile: null,
      result: null,
      error: null
    });
    document.getElementById('fileInput').value = '';
  }

  render() {
    const { selectedFile, loading, result, error } = this.state;

    return (
      <div>
        <h1>Import from Kindle Notes</h1>
        <p className="lead">
          Import vocabulary words from your Kindle notes and highlights file.
        </p>

        <Card className="mb-4">
          <CardHeader>
            <h5 className="mb-0">Import Instructions</h5>
          </CardHeader>
          <CardBody>
            <ul className="mb-0">
              <li>Select your Kindle notes file (e.g., "My Clippings.txt" or exported HTML file)</li>
              <li>The file should contain your Kindle highlights and notes</li>
              <li>Words will be automatically extracted and added to your vocabulary</li>
              <li>Duplicate words will be handled automatically</li>
            </ul>
          </CardBody>
        </Card>

        <Form onSubmit={this.handleSubmit}>
          <FormGroup>
            <Label for="fileInput">Select Kindle Notes File *</Label>
            <Input
              type="file"
              name="fileInput"
              id="fileInput"
              onChange={this.handleFileChange}
              accept=".txt,.html,.htm"
              disabled={loading}
              required
            />
            <small className="form-text text-muted">
              {selectedFile ? `Selected: ${selectedFile.name} (${(selectedFile.size / 1024).toFixed(2)} KB)` : 'No file selected'}
            </small>
          </FormGroup>

          <div className="d-flex gap-2">
            <Button 
              color="primary" 
              type="submit" 
              disabled={loading || !selectedFile}
            >
              {loading ? (
                <>
                  <Spinner size="sm" className="me-2" />
                  Importing...
                </>
              ) : (
                'Import from Kindle'
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
            <div style={{ whiteSpace: 'pre-wrap' }}>
              {result}
            </div>
          </Alert>
        )}

        {loading && (
          <Alert color="info" className="mt-3">
            <Spinner size="sm" className="me-2" />
            Processing Kindle notes file... This may take a moment.
          </Alert>
        )}
      </div>
    );
  }
}
