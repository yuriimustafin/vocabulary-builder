import React, { Component } from 'react';
import { Button, Card, CardBody, CardTitle, CardText, Alert } from 'reactstrap';
import { useNavigate } from 'react-router-dom';

class ListPracticeClass extends Component {
  static displayName = ListPracticeClass.name;

  constructor(props) {
    super(props);
    const language = localStorage.getItem('language') || 'en';
    this.state = {
      lists: [],
      loading: true,
      practicedListIds: [],
      currentList: null,
      mixedItems: [],
      selectedItemIds: new Set(),
      showResult: false,
      score: 0,
      totalCorrect: 0,
      finished: false,
      noAvailableLists: false,
      language: language
    };
  }

  componentDidMount() {
    this.loadLists();
  }

  async loadLists() {
    const { language } = this.state;
    
    try {
      // Fetch all lists
      const response = await fetch(`/api/${language}/lists`);
      const lists = await response.json();
      
      // Filter lists that have items
      const listsWithItems = lists.filter(list => list.itemCount > 0);
      
      if (listsWithItems.length < 2) {
        this.setState({ 
          loading: false,
          error: 'You need at least 2 lists with items to practice.'
        });
        return;
      }

      this.setState({ lists: listsWithItems, loading: false }, () => {
        this.startNextRound();
      });
    } catch (error) {
      console.error('Error loading lists:', error);
      this.setState({ loading: false, error: 'Failed to load lists.' });
    }
  }

  async startNextRound() {
    const { lists, practicedListIds } = this.state;
    
    // Get unpracticed lists
    const unpracticedLists = lists.filter(list => !practicedListIds.includes(list.id));
    
    if (unpracticedLists.length === 0) {
      this.setState({ finished: true });
      return;
    }

    // Pick a random unpracticed list
    const randomIndex = Math.floor(Math.random() * unpracticedLists.length);
    const targetList = unpracticedLists[randomIndex];

    // Load the list details
    await this.loadListDetailsAndMix(targetList);
  }

  async loadListDetailsAndMix(targetList) {
    const { language, lists } = this.state;
    
    try {
      // Fetch target list details
      const response = await fetch(`/api/${language}/lists/${targetList.id}`);
      const listDetails = await response.json();
      
      const targetItems = listDetails.items;
      const targetItemCount = targetItems.length;

      // Create a set of target item texts for efficient lookup (case-insensitive)
      const targetItemTexts = new Set(targetItems.map(item => item.text.toLowerCase()));

      // Get other lists (excluding the target)
      const otherLists = lists.filter(list => list.id !== targetList.id);
      
      // Collect items from other lists, filtering out lists with overlapping items
      const otherItems = [];
      for (const list of otherLists) {
        const otherResponse = await fetch(`/api/${language}/lists/${list.id}`);
        const otherDetails = await otherResponse.json();
        
        // Check if any items from this list overlap with target list
        const hasOverlap = otherDetails.items.some(item => 
          targetItemTexts.has(item.text.toLowerCase())
        );
        
        // Only add items from this list if there's no overlap
        if (!hasOverlap) {
          otherItems.push(...otherDetails.items);
        }
      }

      // Check if we have enough items after filtering
      if (otherItems.length === 0) {
        this.setState({ 
          noAvailableLists: true,
          loading: false
        });
        return;
      }

      // Shuffle and pick random items from other lists
      const shuffledOtherItems = this.shuffleArray([...otherItems]);
      const selectedOtherItems = shuffledOtherItems.slice(0, targetItemCount);

      // Combine and shuffle all items
      const allItems = [...targetItems, ...selectedOtherItems];
      const mixedItems = this.shuffleArray(allItems).map(item => ({
        ...item,
        isFromTargetList: targetItems.some(ti => ti.id === item.id)
      }));

      this.setState({
        currentList: targetList,
        mixedItems: mixedItems,
        selectedItemIds: new Set(),
        showResult: false
      });
    } catch (error) {
      console.error('Error loading list details:', error);
    }
  }

  shuffleArray(array) {
    const shuffled = [...array];
    for (let i = shuffled.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }
    return shuffled;
  }

  toggleItemSelection = (itemId) => {
    if (this.state.showResult) return; // Can't change selection after submit

    this.setState(prevState => {
      const newSelected = new Set(prevState.selectedItemIds);
      if (newSelected.has(itemId)) {
        newSelected.delete(itemId);
      } else {
        newSelected.add(itemId);
      }
      return { selectedItemIds: newSelected };
    });
  }

  handleSubmit = () => {
    const { mixedItems, selectedItemIds } = this.state;
    
    // Calculate score
    const correctItems = mixedItems.filter(item => item.isFromTargetList);
    const correctIds = new Set(correctItems.map(item => item.id));
    
    let correctSelections = 0;
    let incorrectSelections = 0;
    
    selectedItemIds.forEach(id => {
      if (correctIds.has(id)) {
        correctSelections++;
      } else {
        incorrectSelections++;
      }
    });
    
    const missedItems = correctItems.length - correctSelections;
    const score = Math.max(0, correctSelections - incorrectSelections);
    const maxScore = correctItems.length;
    
    this.setState({ 
      showResult: true, 
      score: score,
      totalCorrect: correctSelections,
      maxScore: maxScore,
      incorrectCount: incorrectSelections,
      missedCount: missedItems
    });
  }

  handleNext = () => {
    const { currentList, practicedListIds } = this.state;
    
    this.setState({
      practicedListIds: [...practicedListIds, currentList.id]
    }, () => {
      this.startNextRound();
    });
  }

  handleStartOver = () => {
    this.setState({
      practicedListIds: [],
      finished: false,
      noAvailableLists: false,
      currentList: null,
      mixedItems: [],
      selectedItemIds: new Set(),
      showResult: false
    }, () => {
      this.startNextRound();
    });
  }

  handleGoBack = () => {
    this.props.navigate('/lists');
  }

  render() {
    const { 
      loading, 
      error, 
      currentList, 
      mixedItems, 
      selectedItemIds, 
      showResult, 
      score, 
      totalCorrect,
      maxScore,
      incorrectCount,
      missedCount,
      finished,
      noAvailableLists,
      lists,
      practicedListIds
    } = this.state;

    if (loading) {
      return <p><em>Loading...</em></p>;
    }

    if (error) {
      return (
        <div>
          <Alert color="warning">{error}</Alert>
          <Button color="primary" onClick={this.handleGoBack}>
            Back to Lists
          </Button>
        </div>
      );
    }

    if (noAvailableLists) {
      return (
        <div>
          <h1>Cannot Practice Lists</h1>
          <Alert color="danger">
            All available lists have overlapping items with the selected list. Practice cannot continue.
          </Alert>
          <div className="mt-3">
            <Button color="secondary" onClick={this.handleGoBack}>
              Back to Lists
            </Button>
          </div>
        </div>
      );
    }

    if (finished) {
      return (
        <div>
          <h1>Practice Complete! 🎉</h1>
          <Alert color="success">
            You've practiced all {lists.length} lists!
          </Alert>
          <div className="mt-3">
            <Button color="primary" className="me-2" onClick={this.handleStartOver}>
              Practice Again
            </Button>
            <Button color="secondary" onClick={this.handleGoBack}>
              Back to Lists
            </Button>
          </div>
        </div>
      );
    }

    if (!currentList) {
      return <p><em>Loading practice...</em></p>;
    }

    const progress = `${practicedListIds.length + 1} / ${lists.length}`;

    return (
      <div>
        <div className="d-flex justify-content-between align-items-center mb-3">
          <h1>Practice Lists</h1>
          <div>
            <span className="me-3">Progress: {progress}</span>
            <Button color="secondary" size="sm" onClick={this.handleGoBack}>
              Exit Practice
            </Button>
          </div>
        </div>

        <Card className="mb-3">
          <CardBody>
            <CardTitle tag="h3">{currentList.title}</CardTitle>
            <CardText>
              Select all items that belong to this list:
            </CardText>
          </CardBody>
        </Card>

        {showResult && (
          <Alert color={score === maxScore ? 'success' : 'info'}>
            <h5>Results:</h5>
            <p className="mb-1">✓ Correct selections: {totalCorrect} / {maxScore}</p>
            {incorrectCount > 0 && <p className="mb-1">✗ Incorrect selections: {incorrectCount}</p>}
            {missedCount > 0 && <p className="mb-1">○ Missed items: {missedCount}</p>}
            <p className="mb-0"><strong>Score: {score} / {maxScore}</strong></p>
          </Alert>
        )}

        <div className="row">
          {mixedItems.map(item => {
            const isSelected = selectedItemIds.has(item.id);
            const showCorrectness = showResult;
            const isCorrect = item.isFromTargetList;
            
            let cardClass = 'mb-3';
            let borderColor = '';
            
            if (showCorrectness) {
              if (isCorrect) {
                borderColor = isSelected ? 'border-success' : 'border-warning';
                cardClass += ` ${borderColor} border-2`;
              } else if (isSelected) {
                borderColor = 'border-danger';
                cardClass += ` ${borderColor} border-2`;
              }
            } else if (isSelected) {
              cardClass += ' border-primary border-2';
            }

            return (
              <div key={item.id} className="col-md-4 col-sm-6">
                <Card 
                  className={cardClass}
                  style={{ cursor: showResult ? 'default' : 'pointer' }}
                  onClick={() => this.toggleItemSelection(item.id)}
                >
                  <CardBody>
                    <CardText>
                      <span style={{ fontSize: '1.1rem' }}>{item.text}</span>
                      {showCorrectness && (
                        <span className="ms-2">
                          {isCorrect && isSelected && '✓'}
                          {isCorrect && !isSelected && '○'}
                          {!isCorrect && isSelected && '✗'}
                        </span>
                      )}
                    </CardText>
                  </CardBody>
                </Card>
              </div>
            );
          })}
        </div>

        <div className="mt-3">
          {!showResult ? (
            <Button 
              color="primary" 
              size="lg"
              onClick={this.handleSubmit}
              disabled={selectedItemIds.size === 0}
            >
              Submit
            </Button>
          ) : (
            <Button color="primary" size="lg" onClick={this.handleNext}>
              Next List
            </Button>
          )}
        </div>
      </div>
    );
  }
}

// Wrapper to use hooks with class component
export function ListPractice() {
  const navigate = useNavigate();
  return <ListPracticeClass navigate={navigate} />;
}
