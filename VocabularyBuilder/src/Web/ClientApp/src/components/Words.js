import React, { Component } from 'react';
import { Button, Modal, ModalHeader, ModalBody, ModalFooter, Form, FormGroup, Label, Input, Table, Dropdown, DropdownToggle, DropdownMenu, DropdownItem } from 'reactstrap';
import { Link } from 'react-router-dom';
import { WordsClient } from '../web-api-client.ts';

export class Words extends Component {
  static displayName = Words.name;

  constructor(props) {
    super(props);
    this.state = {
      words: [],
      loading: true,
      modal: false,
      deleteModal: false,
      detailsModal: false,
      currentWord: null,
      wordDetails: null,
      loadingDetails: false,
      sortBy: null,
      dropdownOpen: false,
      formData: {
        id: 0,
        headword: '',
        transcription: '',
        partOfSpeech: '',
        frequency: '',
        examples: ''
      },
      isEditing: false
    };
  }

  componentDidMount() {
    this.loadWords();
  }

  async loadWords(sortBy = null) {
    try {
      const client = new WordsClient();
      const data = await client.getWords(sortBy);
      this.setState({ words: data, loading: false, sortBy });
    } catch (error) {
      console.error('Error loading words:', error);
      this.setState({ loading: false });
    }
  }

  handleSortChange = (sortBy) => {
    this.setState({ loading: true });
    this.loadWords(sortBy);
  }

  toggleDropdown = () => {
    this.setState(prevState => ({
      dropdownOpen: !prevState.dropdownOpen
    }));
  }

  getSortLabel = () => {
    const { sortBy } = this.state;
    switch (sortBy) {
      case 'frequency':
        return 'Frequency';
      case 'lastencounter':
        return 'Last Encounter';
      case 'created':
        return 'Created Date';
      default:
        return 'Alphabetical';
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
      case 0: return 'secondary'; // New
      case 1: return 'primary';   // Next Export
      case 2: return 'info';      // Exported
      case 3: return 'warning';   // Learned
      case 4: return 'success';   // Known
      default: return 'dark';
    }
  }

  handleMarkAsKnown = async (word) => {
    try {
      const client = new WordsClient();
      await client.updateWordStatus(word.id, { id: word.id, status: 4 }); // 4 = Known
      this.loadWords(this.state.sortBy);
    } catch (error) {
      console.error('Error updating word status:', error);
      alert('Error updating word status. Please try again.');
    }
  }

  handleStatusChange = async (wordId, newStatus) => {
    try {
      const client = new WordsClient();
      await client.updateWordStatus(wordId, { id: wordId, status: parseInt(newStatus) });
      // Reload both word list and details
      this.loadWords(this.state.sortBy);
      
      // If details modal is open, reload details
      if (this.state.detailsModal && this.state.wordDetails) {
        const response = await fetch(`/api/Words/${wordId}/details`);
        if (response.ok) {
          const details = await response.json();
          this.setState({ wordDetails: details });
        }
      }
    } catch (error) {
      console.error('Error updating word status:', error);
      alert('Error updating word status. Please try again.');
    }
  }

  toggleModal = () => {
    this.setState(prevState => ({
      modal: !prevState.modal,
      formData: {
        id: 0,
        headword: '',
        transcription: '',
        partOfSpeech: '',
        frequency: '',
        examples: ''
      },
      isEditing: false
    }));
  }

  toggleDeleteModal = (word = null) => {
    this.setState(prevState => ({
      deleteModal: !prevState.deleteModal,
      currentWord: word
    }));
  }

  toggleDetailsModal = async (word = null) => {
    if (word && !this.state.detailsModal) {
      // Opening modal - fetch details
      this.setState({ detailsModal: true, loadingDetails: true });
      try {
        const response = await fetch(`/api/Words/${word.id}/details`);
        if (response.ok) {
          const details = await response.json();
          this.setState({ wordDetails: details, loadingDetails: false });
        } else {
          console.error('Failed to load word details');
          this.setState({ loadingDetails: false });
        }
      } catch (error) {
        console.error('Error loading word details:', error);
        this.setState({ loadingDetails: false });
      }
    } else {
      // Closing modal
      this.setState({ detailsModal: false, wordDetails: null });
    }
  }

  handleInputChange = (e) => {
    const { name, value } = e.target;
    this.setState(prevState => ({
      formData: {
        ...prevState.formData,
        [name]: value
      }
    }));
  }

  handleEdit = (word) => {
    this.setState({
      modal: true,
      isEditing: true,
      formData: {
        id: word.id,
        headword: word.headword || '',
        transcription: word.transcription || '',
        partOfSpeech: word.partOfSpeech || '',
        frequency: word.frequency || '',
        examples: word.examples ? word.examples.join('\n') : ''
      }
    });
  }

  handleSubmit = async (e) => {
    e.preventDefault();
    const client = new WordsClient();
    
    try {
      const { formData, isEditing } = this.state;
      const examplesArray = formData.examples 
        ? formData.examples.split('\n').filter(e => e.trim() !== '')
        : [];

      const command = {
        headword: formData.headword,
        transcription: formData.transcription || null,
        partOfSpeech: formData.partOfSpeech || null,
        frequency: formData.frequency ? parseInt(formData.frequency) : null,
        examples: examplesArray.length > 0 ? examplesArray : null
      };

      if (isEditing) {
        await client.updateWord(formData.id, { ...command, id: formData.id });
      } else {
        await client.createWord(command);
      }

      this.toggleModal();
      this.loadWords();
    } catch (error) {
      console.error('Error saving word:', error);
      alert('Error saving word. Please try again.');
    }
  }

  handleDelete = async () => {
    const client = new WordsClient();
    
    try {
      await client.deleteWord(this.state.currentWord.id);
      this.toggleDeleteModal();
      this.loadWords();
    } catch (error) {
      console.error('Error deleting word:', error);
      alert('Error deleting word. Please try again.');
    }
  }

  render() {
    const { words, loading, modal, deleteModal, detailsModal, formData, isEditing, currentWord, wordDetails, loadingDetails } = this.state;

    if (loading) {
      return <p><em>Loading...</em></p>;
    }

    return (
      <div>
        <div className="d-flex justify-content-between align-items-center mb-3">
          <h1>Words</h1>
          <div className="d-flex gap-2">
            <Dropdown isOpen={this.state.dropdownOpen} toggle={this.toggleDropdown}>
              <DropdownToggle caret color="secondary">
                Sort by: {this.getSortLabel()}
              </DropdownToggle>
              <DropdownMenu>
                <DropdownItem onClick={() => this.handleSortChange(null)}>
                  Alphabetical
                </DropdownItem>
                <DropdownItem onClick={() => this.handleSortChange('frequency')}>
                  Frequency
                </DropdownItem>
                <DropdownItem onClick={() => this.handleSortChange('lastencounter')}>
                  Last Encounter Date
                </DropdownItem>
                <DropdownItem onClick={() => this.handleSortChange('created')}>
                  Created Date
                </DropdownItem>
              </DropdownMenu>
            </Dropdown>
            <Button color="success" tag={Link} to="/bulk-import">
              Bulk Import
            </Button>
            <Button color="info" tag={Link} to="/kindle-import">
              Import from Kindle
            </Button>
            <Button color="primary" onClick={this.toggleModal}>
              Add New Word
            </Button>
          </div>
        </div>

        <Table striped hover>
          <thead>
            <tr>
              <th>Headword</th>
              <th>Part of Speech</th>
              <th>Frequency</th>
              <th>Status</th>
              <th>Encounter Count</th>
              <th>Known</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {words.map(word => (
              <tr key={word.id}>
                <td>{word.headword}</td>
                <td>{word.partOfSpeech || '-'}</td>
                <td>{word.frequency || '-'}</td>
                <td>
                  <span className={`badge bg-${this.getStatusColor(word.status)}`}>
                    {this.getStatusLabel(word.status)}
                  </span>
                </td>
                <td>{word.encounterCount}</td>
                <td>
                  {word.status !== 4 ? (
                    <Button 
                      color="success" 
                      size="sm"
                      onClick={() => this.handleMarkAsKnown(word)}
                    >
                      Mark as Known
                    </Button>
                  ) : (
                    <span className="text-success">✓</span>
                  )}
                </td>
                <td>
                  <Button 
                    color="primary" 
                    size="sm" 
                    className="me-2"
                    onClick={() => this.toggleDetailsModal(word)}
                  >
                    View
                  </Button>
                  <Button 
                    color="info" 
                    size="sm" 
                    className="me-2"
                    onClick={() => this.handleEdit(word)}
                  >
                    Edit
                  </Button>
                  <Button 
                    color="danger" 
                    size="sm"
                    onClick={() => this.toggleDeleteModal(word)}
                  >
                    Delete
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>

        {words.length === 0 && (
          <p className="text-center">No words found. Add your first word!</p>
        )}

        {/* Add/Edit Modal */}
        <Modal isOpen={modal} toggle={this.toggleModal} size="lg">
          <ModalHeader toggle={this.toggleModal}>
            {isEditing ? 'Edit Word' : 'Add New Word'}
          </ModalHeader>
          <ModalBody>
            <Form onSubmit={this.handleSubmit}>
              <FormGroup>
                <Label for="headword">Headword *</Label>
                <Input
                  type="text"
                  name="headword"
                  id="headword"
                  value={formData.headword}
                  onChange={this.handleInputChange}
                  required
                />
              </FormGroup>
              <FormGroup>
                <Label for="transcription">Transcription</Label>
                <Input
                  type="text"
                  name="transcription"
                  id="transcription"
                  value={formData.transcription}
                  onChange={this.handleInputChange}
                  placeholder="e.g., /həˈləʊ/"
                />
              </FormGroup>
              <FormGroup>
                <Label for="partOfSpeech">Part of Speech</Label>
                <Input
                  type="text"
                  name="partOfSpeech"
                  id="partOfSpeech"
                  value={formData.partOfSpeech}
                  onChange={this.handleInputChange}
                  placeholder="e.g., noun, verb, adjective"
                />
              </FormGroup>
              <FormGroup>
                <Label for="frequency">Frequency</Label>
                <Input
                  type="number"
                  name="frequency"
                  id="frequency"
                  value={formData.frequency}
                  onChange={this.handleInputChange}
                />
              </FormGroup>
              <FormGroup>
                <Label for="examples">Examples (one per line)</Label>
                <Input
                  type="textarea"
                  name="examples"
                  id="examples"
                  value={formData.examples}
                  onChange={this.handleInputChange}
                  rows="4"
                  placeholder="Enter each example on a new line"
                />
              </FormGroup>
            </Form>
          </ModalBody>
          <ModalFooter>
            <Button color="primary" onClick={this.handleSubmit}>
              {isEditing ? 'Update' : 'Create'}
            </Button>
            <Button color="secondary" onClick={this.toggleModal}>
              Cancel
            </Button>
          </ModalFooter>
        </Modal>

        {/* Delete Confirmation Modal */}
        <Modal isOpen={deleteModal} toggle={() => this.toggleDeleteModal()}>
          <ModalHeader toggle={() => this.toggleDeleteModal()}>
            Confirm Delete
          </ModalHeader>
          <ModalBody>
            Are you sure you want to delete the word "{currentWord?.headword}"?
          </ModalBody>
          <ModalFooter>
            <Button color="danger" onClick={this.handleDelete}>
              Delete
            </Button>
            <Button color="secondary" onClick={() => this.toggleDeleteModal()}>
              Cancel
            </Button>
          </ModalFooter>
        </Modal>

        {/* Word Details Modal */}
        <Modal isOpen={detailsModal} toggle={() => this.toggleDetailsModal()} size="xl">
          <ModalHeader toggle={() => this.toggleDetailsModal()}>
            Word Details
          </ModalHeader>
          <ModalBody>
            {loadingDetails && (
              <div className="text-center">
                <p>Loading...</p>
              </div>
            )}
            {!loadingDetails && wordDetails && (
              <div>
                <h3>{wordDetails.headword}</h3>
                {wordDetails.transcription && (
                  <p className="text-muted">{wordDetails.transcription}</p>
                )}
                
                <hr />
                
                <div className="row">
                  <div className="col-md-4">
                    <strong>Part of Speech:</strong> {wordDetails.partOfSpeech || 'N/A'}
                  </div>
                  <div className="col-md-4">
                    <strong>Frequency:</strong> {wordDetails.frequency || 'N/A'}
                  </div>
                  <div className="col-md-4">
                    <strong>Status:</strong>
                    <Input
                      type="select"
                      value={wordDetails.status}
                      onChange={(e) => this.handleStatusChange(wordDetails.id, e.target.value)}
                      className="d-inline-block ms-2"
                      style={{ width: 'auto' }}
                    >
                      <option value="0">New</option>
                      <option value="1">Exported</option>
                      <option value="2">Learned</option>
                      <option value="3">Known</option>
                    </Input>
                  </div>
                </div>

                {wordDetails.senses && wordDetails.senses.length > 0 && (
                  <>
                    <h5 className="mt-4">Definitions</h5>
                    {wordDetails.senses.map((sense, index) => (
                      <div key={index} className="mb-3 p-3 border rounded">
                        <strong>{index + 1}. {sense.partOfSpeech}</strong>
                        <p className="mb-2">{sense.definition}</p>
                        {sense.examples && sense.examples.length > 0 && (
                          <div className="ms-3">
                            <small className="text-muted">Examples:</small>
                            <ul className="mb-0">
                              {sense.examples.map((ex, i) => (
                                <li key={i}><small>{ex}</small></li>
                              ))}
                            </ul>
                          </div>
                        )}
                      </div>
                    ))}
                  </>
                )}

                {wordDetails.examples && wordDetails.examples.length > 0 && (
                  <>
                    <h5 className="mt-4">Examples</h5>
                    <ul>
                      {wordDetails.examples.map((ex, index) => (
                        <li key={index}>{ex}</li>
                      ))}
                    </ul>
                  </>
                )}

                {wordDetails.encounters && wordDetails.encounters.length > 0 && (
                  <>
                    <h5 className="mt-4">Encounters ({wordDetails.encounters.length})</h5>
                    <div className="table-responsive">
                      <table className="table table-sm">
                        <thead>
                          <tr>
                            <th>Source</th>
                            <th>Context</th>
                            <th>Notes</th>
                            <th>Date</th>
                          </tr>
                        </thead>
                        <tbody>
                          {wordDetails.encounters.map((enc, index) => (
                            <tr key={index}>
                              <td><span className="badge bg-secondary">{enc.source}</span></td>
                              <td>{enc.context || '-'}</td>
                              <td>{enc.notes || '-'}</td>
                              <td><small>{new Date(enc.encounteredAt).toLocaleDateString()}</small></td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </>
                )}

                {wordDetails.dictionarySources && wordDetails.dictionarySources.length > 0 && (
                  <>
                    <h5 className="mt-4">Dictionary Sources</h5>
                    <ul>
                      {wordDetails.dictionarySources.map((source, index) => (
                        <li key={index}>
                          {source.sourceType}
                          {source.sourceUrl && (
                            <> - <a href={source.sourceUrl} target="_blank" rel="noopener noreferrer">View</a></>
                          )}
                        </li>
                      ))}
                    </ul>
                  </>
                )}
              </div>
            )}
          </ModalBody>
          <ModalFooter>
            <Button color="secondary" onClick={() => this.toggleDetailsModal()}>
              Close
            </Button>
          </ModalFooter>
        </Modal>
      </div>
    );
  }
}
