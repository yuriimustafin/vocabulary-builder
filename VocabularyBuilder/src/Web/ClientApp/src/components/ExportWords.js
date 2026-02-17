import React, { Component } from 'react';
import { Button, Table, Alert, Spinner } from 'reactstrap';
import { Link } from 'react-router-dom';

export class ExportWords extends Component {
  static displayName = ExportWords.name;

  constructor(props) {
    super(props);
    this.state = {
      words: [],
      loading: true,
      exporting: false,
      error: null,
      selectedStatuses: [1] // Default: NextExport
    };
  }

  componentDidMount() {
    this.loadWordsForExport();
  }

  async loadWordsForExport() {
    try {
      const { selectedStatuses } = this.state;
      const statusesParam = selectedStatuses.length > 0 
        ? selectedStatuses.map(s => `statuses=${s}`).join('&')
        : '';
      
      const response = await fetch(`/api/Words/for-export?${statusesParam}`);
      
      if (response.ok) {
        const data = await response.json();
        this.setState({ 
          words: data || [], 
          loading: false 
        });
      } else {
        this.setState({ 
          loading: false, 
          error: 'Failed to load words for export' 
        });
      }
    } catch (error) {
      console.error('Error loading words for export:', error);
      this.setState({ 
        loading: false, 
        error: 'Error loading words for export' 
      });
    }
  }

  async handleExport() {
    const { words } = this.state;
    
    if (words.length === 0) {
      alert('No words to export');
      return;
    }

    this.setState({ exporting: true, error: null });

    try {
      const wordIds = words.map(w => w.id);
      
      const response = await fetch('/api/Words/export', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ wordIds })
      });

      if (response.ok) {
        // Get the CSV file
        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        
        // Extract filename from Content-Disposition header if available
        const contentDisposition = response.headers.get('Content-Disposition');
        let filename = `anki-export-${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}.csv`;
        
        if (contentDisposition) {
          // Match filename="value" or filename=value (before semicolon or end of string)
          const filenameMatch = contentDisposition.match(/filename[^;=\n]*=["']?([^"';\n]+)["']?/i);
          if (filenameMatch && filenameMatch[1]) {
            filename = filenameMatch[1].trim();
          }
        }
        
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);

        // Show success message and redirect
        alert(`Successfully exported ${words.length} word(s) to Anki CSV format!`);
        window.location.href = '/words';
      } else {
        const errorText = await response.text();
        this.setState({ 
          exporting: false, 
          error: errorText || 'Failed to export words' 
        });
      }
    } catch (error) {
      console.error('Error exporting words:', error);
      this.setState({ 
        exporting: false, 
        error: 'Error exporting words. Please try again.' 
      });
    }
  }

  getStatusLabel = (status) => {
    switch (status) {
      case 0: return 'New';
      case 1: return 'Next Export';
      case 2: return 'Exported';
      case 3: return 'Learned';
      case 4: return 'Known';
      default: return 'Unknown';
    }
  }

  getStatusColor = (status) => {
    switch (status) {
      case 0: return 'secondary';
      case 1: return 'primary';
      case 2: return 'info';
      case 3: return 'warning';
      case 4: return 'success';
      default: return 'dark';
    }
  }

  render() {
    const { words, loading, exporting, error } = this.state;

    if (loading) {
      return (
        <div className="text-center mt-5">
          <Spinner color="primary" />
          <p className="mt-3">Loading words for export...</p>
        </div>
      );
    }

    return (
      <div className="container mt-4">
        <div className="d-flex justify-content-between align-items-center mb-4">
          <h1>Export Words to Anki</h1>
          <Button color="secondary" tag={Link} to="/words">
            Back to Words
          </Button>
        </div>

        {error && (
          <Alert color="danger" className="mb-4">
            {error}
          </Alert>
        )}

        {words.length === 0 ? (
          <Alert color="info">
            <h4 className="alert-heading">No words to export</h4>
            <p>
              There are no words with "Next Export" status. 
              Please update word statuses on the Words page first.
            </p>
            <hr />
            <p className="mb-0">
              <Button color="primary" tag={Link} to="/words">
                Go to Words Page
              </Button>
            </p>
          </Alert>
        ) : (
          <>
            <Alert color="info" className="mb-4">
              <strong>{words.length}</strong> word{words.length !== 1 ? 's' : ''} will be exported.
              After export, all words will be marked as "Exported".
            </Alert>

            <div className="mb-4">
              <Button 
                color="success" 
                size="lg"
                onClick={() => this.handleExport()}
                disabled={exporting}
              >
                {exporting ? (
                  <>
                    <Spinner size="sm" className="me-2" />
                    Exporting...
                  </>
                ) : (
                  <>
                    Generate & Download CSV
                  </>
                )}
              </Button>
            </div>

            <h3 className="mb-3">Preview</h3>
            <Table striped hover>
              <thead>
                <tr>
                  <th>#</th>
                  <th>Headword</th>
                  <th>Part of Speech</th>
                  <th>Status</th>
                  <th>Senses</th>
                </tr>
              </thead>
              <tbody>
                {words.map((word, index) => (
                  <tr key={word.id}>
                    <td>{index + 1}</td>
                    <td><strong>{word.headword}</strong></td>
                    <td>{word.partOfSpeech || '-'}</td>
                    <td>
                      <span className={`badge bg-${this.getStatusColor(word.status)}`}>
                        {this.getStatusLabel(word.status)}
                      </span>
                    </td>
                    <td>{word.senseCount}</td>
                  </tr>
                ))}
              </tbody>
            </Table>
          </>
        )}
      </div>
    );
  }
}
