import React, { Component } from 'react';
import { Button, Modal, ModalHeader, ModalBody, ModalFooter, Form, FormGroup, Label, Input, Table } from 'reactstrap';
import { Link } from 'react-router-dom';

export class Lists extends Component {
  static displayName = Lists.name;

  constructor(props) {
    super(props);
    const language = localStorage.getItem('language') || 'en';
    this.state = {
      lists: [],
      loading: true,
      modal: false,
      deleteModal: false,
      detailsModal: false,
      currentList: null,
      listDetails: null,
      loadingDetails: false,
      isEditing: false,
      formData: {
        title: '',
        status: '0'
      },
      // Item form data
      itemFormData: {
        text: '',
        isMastered: false
      },
      itemModal: false,
      editingItem: null,
      language: language
    };
  }

  componentDidMount() {
    this.loadLists();
  }

  componentDidUpdate(prevProps, prevState) {
    // Reload lists if language changed
    const newLanguage = localStorage.getItem('language') || 'en';
    if (newLanguage !== prevState.language) {
      this.setState({ language: newLanguage }, () => {
        this.loadLists();
      });
    }
  }

  async loadLists() {
    this.setState({ loading: true });
    const language = this.state.language;
    
    try {
      const response = await fetch(`/api/${language}/lists`);
      const data = await response.json();
      this.setState({ lists: data, loading: false });
    } catch (error) {
      console.error('Error loading lists:', error);
      this.setState({ loading: false });
    }
  }

  async loadListDetails(listId) {
    this.setState({ loadingDetails: true });
    const language = this.state.language;
    
    try {
      const response = await fetch(`/api/${language}/lists/${listId}`);
      const data = await response.json();
      this.setState({ listDetails: data, loadingDetails: false });
    } catch (error) {
      console.error('Error loading list details:', error);
      this.setState({ loadingDetails: false });
    }
  }

  toggleModal = () => {
    this.setState(prevState => ({
      modal: !prevState.modal,
      formData: {
        title: '',
        status: '0'
      },
      isEditing: false,
      currentList: null
    }));
  }

  toggleDeleteModal = (list = null) => {
    this.setState(prevState => ({
      deleteModal: !prevState.deleteModal,
      currentList: list
    }));
  }

  toggleDetailsModal = (list = null) => {
    if (list) {
      this.loadListDetails(list.id);
    }
    this.setState(prevState => ({
      detailsModal: !prevState.detailsModal,
      currentList: list,
      listDetails: null
    }));
  }

  toggleItemModal = (item = null) => {
    this.setState(prevState => ({
      itemModal: !prevState.itemModal,
      editingItem: item,
      itemFormData: item ? {
        text: item.text,
        isMastered: item.isMastered
      } : {
        text: '',
        isMastered: false
      }
    }));
  }

  handleEdit = (list) => {
    this.setState({
      currentList: list,
      isEditing: true,
      formData: {
        title: list.title,
        status: list.status.toString()
      },
      modal: true
    });
  }

  handleInputChange = (event) => {
    const { name, value, type, checked } = event.target;
    this.setState(prevState => ({
      formData: {
        ...prevState.formData,
        [name]: type === 'checkbox' ? checked : value
      }
    }));
  }

  handleItemInputChange = (event) => {
    const { name, value, type, checked } = event.target;
    this.setState(prevState => ({
      itemFormData: {
        ...prevState.itemFormData,
        [name]: type === 'checkbox' ? checked : value
      }
    }));
  }

  handleSubmit = async (e) => {
    e.preventDefault();
    const { isEditing, currentList, formData, language } = this.state;
    
    try {
      if (isEditing) {
        // Update existing list
        const response = await fetch(`/api/${language}/lists/${currentList.id}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            id: currentList.id,
            title: formData.title,
            status: parseInt(formData.status)
          }),
        });
        
        if (!response.ok) {
          console.error('Failed to update list:', response.status, response.statusText);
          throw new Error(`Failed to update list: ${response.status}`);
        }
      } else {
        // Create new list
        const response = await fetch(`/api/${language}/lists`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            title: formData.title,
            status: parseInt(formData.status)
          }),
        });
        
        if (!response.ok) {
          console.error('Failed to create list:', response.status, response.statusText);
          throw new Error(`Failed to create list: ${response.status}`);
        }
      }
      
      this.toggleModal();
      await this.loadLists();
    } catch (error) {
      console.error('Error saving list:', error);
      alert(`Error saving list: ${error.message}`);
    }
  }

  handleItemSubmit = async (e) => {
    e.preventDefault();
    const { currentList, editingItem, itemFormData, language } = this.state;
    
    try {
      if (editingItem) {
        // Update existing item
        await fetch(`/api/${language}/lists/${currentList.id}/items/${editingItem.id}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            id: editingItem.id,
            text: itemFormData.text,
            isMastered: itemFormData.isMastered
          }),
        });
      } else {
        // Create new item
        await fetch(`/api/${language}/lists/${currentList.id}/items`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            text: itemFormData.text,
            isMastered: itemFormData.isMastered
          }),
        });
      }
      
      this.toggleItemModal();
      this.loadListDetails(currentList.id);
    } catch (error) {
      console.error('Error saving item:', error);
    }
  }

  handleDelete = async () => {
    const { currentList, language } = this.state;
    
    try {
      await fetch(`/api/${language}/lists/${currentList.id}`, {
        method: 'DELETE',
      });
      
      this.toggleDeleteModal();
      this.loadLists();
    } catch (error) {
      console.error('Error deleting list:', error);
    }
  }

  handleDeleteItem = async (itemId) => {
    const { currentList, language } = this.state;
    
    if (!window.confirm('Are you sure you want to delete this item?')) {
      return;
    }
    
    try {
      await fetch(`/api/${language}/lists/${currentList.id}/items/${itemId}`, {
        method: 'DELETE',
      });
      
      this.loadListDetails(currentList.id);
    } catch (error) {
      console.error('Error deleting item:', error);
    }
  }

  toggleItemMastered = async (item) => {
    const { currentList, language } = this.state;
    
    try {
      await fetch(`/api/${language}/lists/${currentList.id}/items/${item.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          id: item.id,
          text: item.text,
          isMastered: !item.isMastered
        }),
      });
      
      this.loadListDetails(currentList.id);
    } catch (error) {
      console.error('Error updating item:', error);
    }
  }

  getStatusLabel(status) {
    switch (status) {
      case 0: return 'Active';
      case 1: return 'Completed';
      case 2: return 'Archived';
      default: return 'Unknown';
    }
  }

  getStatusColor(status) {
    switch (status) {
      case 0: return 'success';
      case 1: return 'info';
      case 2: return 'secondary';
      default: return 'light';
    }
  }

  render() {
    const { lists, loading, modal, deleteModal, detailsModal, formData, isEditing, currentList, listDetails, loadingDetails, itemModal, itemFormData, editingItem } = this.state;

    if (loading) {
      return <p><em>Loading...</em></p>;
    }

    return (
      <div>
        <div className="d-flex justify-content-between align-items-center mb-3">
          <h1>Vocabulary Lists</h1>
          <div>
            {lists.filter(list => list.itemCount > 0).length >= 2 && (
              <Button 
                color="success" 
                className="me-2"
                tag={Link}
                to="/lists/practice"
              >
                Practice Lists
              </Button>
            )}
            <Button color="primary" onClick={this.toggleModal}>
              Add New List
            </Button>
          </div>
        </div>

        <Table striped hover>
          <thead>
            <tr>
              <th>Title</th>
              <th>Status</th>
              <th>Items</th>
              <th>Mastered</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {lists.map(list => (
              <tr key={list.id}>
                <td><strong>{list.title}</strong></td>
                <td>
                  <span className={`badge bg-${this.getStatusColor(list.status)}`}>
                    {this.getStatusLabel(list.status)}
                  </span>
                </td>
                <td>{list.itemCount}</td>
                <td>{list.masteredCount}</td>
                <td>{new Date(list.created).toLocaleDateString()}</td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  <Button 
                    color="primary" 
                    size="sm" 
                    className="me-1"
                    onClick={() => this.toggleDetailsModal(list)}
                  >
                    View
                  </Button>
                  <Button 
                    color="info" 
                    size="sm" 
                    className="me-1"
                    onClick={() => this.handleEdit(list)}
                  >
                    Edit
                  </Button>
                  <Button 
                    color="danger" 
                    size="sm"
                    onClick={() => this.toggleDeleteModal(list)}
                  >
                    Delete
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>

        {lists.length === 0 && (
          <p className="text-center">No lists found. Add your first list!</p>
        )}

        {/* Add/Edit List Modal */}
        <Modal isOpen={modal} toggle={this.toggleModal}>
          <ModalHeader toggle={this.toggleModal}>
            {isEditing ? 'Edit List' : 'Add New List'}
          </ModalHeader>
          <ModalBody>
            <Form onSubmit={this.handleSubmit}>
              <FormGroup>
                <Label for="title">Title *</Label>
                <Input
                  type="text"
                  name="title"
                  id="title"
                  value={formData.title}
                  onChange={this.handleInputChange}
                  required
                />
              </FormGroup>
              <FormGroup>
                <Label for="status">Status</Label>
                <Input
                  type="select"
                  name="status"
                  id="status"
                  value={formData.status}
                  onChange={this.handleInputChange}
                >
                  <option value="0">Active</option>
                  <option value="1">Completed</option>
                  <option value="2">Archived</option>
                </Input>
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
        <Modal isOpen={deleteModal} toggle={this.toggleDeleteModal}>
          <ModalHeader toggle={this.toggleDeleteModal}>
            Confirm Delete
          </ModalHeader>
          <ModalBody>
            Are you sure you want to delete the list "{currentList?.title}"? This will also delete all items in the list.
          </ModalBody>
          <ModalFooter>
            <Button color="danger" onClick={this.handleDelete}>
              Confirm
            </Button>
            <Button color="secondary" onClick={this.toggleDeleteModal}>
              Cancel
            </Button>
          </ModalFooter>
        </Modal>

        {/* List Details Modal */}
        <Modal isOpen={detailsModal} toggle={() => this.toggleDetailsModal()} size="lg">
          <ModalHeader toggle={() => this.toggleDetailsModal()}>
            {currentList?.title}
          </ModalHeader>
          <ModalBody>
            {loadingDetails ? (
              <p>Loading...</p>
            ) : listDetails ? (
              <div>
                <div className="d-flex justify-content-between align-items-center mb-3">
                  <h5>Items ({listDetails.items.length})</h5>
                  <Button color="success" size="sm" onClick={() => this.toggleItemModal()}>
                    Add Item
                  </Button>
                </div>
                
                {listDetails.items.length === 0 ? (
                  <p className="text-muted">No items in this list yet.</p>
                ) : (
                  <Table size="sm" striped>
                    <thead>
                      <tr>
                        <th style={{ width: '40px' }}></th>
                        <th>Text</th>
                        <th>Created</th>
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {listDetails.items.map(item => (
                        <tr key={item.id} className={item.isMastered ? 'table-success' : ''}>
                          <td>
                            <Input
                              type="checkbox"
                              checked={item.isMastered}
                              onChange={() => this.toggleItemMastered(item)}
                            />
                          </td>
                          <td style={{ textDecoration: item.isMastered ? 'line-through' : 'none' }}>
                            {item.text}
                          </td>
                          <td>{new Date(item.created).toLocaleDateString()}</td>
                          <td style={{ whiteSpace: 'nowrap' }}>
                            <Button 
                              color="info" 
                              size="sm" 
                              className="me-1"
                              onClick={() => this.toggleItemModal(item)}
                            >
                              Edit
                            </Button>
                            <Button 
                              color="danger" 
                              size="sm"
                              onClick={() => this.handleDeleteItem(item.id)}
                            >
                              Delete
                            </Button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                )}
              </div>
            ) : null}
          </ModalBody>
          <ModalFooter>
            <Button color="secondary" onClick={() => this.toggleDetailsModal()}>
              Close
            </Button>
          </ModalFooter>
        </Modal>

        {/* Add/Edit Item Modal */}
        <Modal isOpen={itemModal} toggle={() => this.toggleItemModal()}>
          <ModalHeader toggle={() => this.toggleItemModal()}>
            {editingItem ? 'Edit Item' : 'Add Item'}
          </ModalHeader>
          <ModalBody>
            <Form onSubmit={this.handleItemSubmit}>
              <FormGroup>
                <Label for="text">Word/Phrase *</Label>
                <Input
                  type="text"
                  name="text"
                  id="text"
                  value={itemFormData.text}
                  onChange={this.handleItemInputChange}
                  required
                />
              </FormGroup>
              <FormGroup check>
                <Input
                  type="checkbox"
                  name="isMastered"
                  id="isMastered"
                  checked={itemFormData.isMastered}
                  onChange={this.handleItemInputChange}
                />
                <Label for="isMastered" check>
                  Mastered
                </Label>
              </FormGroup>
            </Form>
          </ModalBody>
          <ModalFooter>
            <Button color="primary" onClick={this.handleItemSubmit}>
              {editingItem ? 'Update' : 'Add'}
            </Button>
            <Button color="secondary" onClick={() => this.toggleItemModal()}>
              Cancel
            </Button>
          </ModalFooter>
        </Modal>
      </div>
    );
  }
}
