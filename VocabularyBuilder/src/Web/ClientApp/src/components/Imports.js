import React, { Component } from 'react';
import { Alert, Badge, Button, ButtonGroup, Modal, ModalBody, ModalFooter, ModalHeader, Table } from 'reactstrap';
import { Link } from 'react-router-dom';
import { currentLanguage, formatDateTime, kindLabel, statusColor } from './historyFormat';

const PAGE_SIZE = 20;

/**
 * Every import run for the current language, newest first, each with a popup listing the
 * words it brought in.
 *
 * A word shows in every import that touched it: "New" where that import created it, and
 * "Already known" where it landed on a word that was there before. Terms the import set
 * aside are listed after the words, with the reason.
 *
 * Opened with ?open=<id> it shows that import's popup straight away, which is how the import
 * pages link to the import they have just run.
 */
export class Imports extends Component {
  static displayName = Imports.name;

  constructor(props) {
    super(props);
    this.state = {
      imports: [],
      loading: true,
      error: null,
      pageNumber: 1,
      totalPages: 0,
      totalCount: 0,
      details: null,
      detailsOpen: false,
      loadingDetails: false,
      filter: 'all'
    };
  }

  componentDidMount() {
    this.loadImports(1);

    const open = new URLSearchParams(window.location.search).get('open');
    if (open) {
      this.openImport(parseInt(open, 10));
    }
  }

  async loadImports(pageNumber) {
    this.setState({ loading: true, error: null });

    try {
      const response = await fetch(`/api/${currentLanguage()}/imports?pageNumber=${pageNumber}&pageSize=${PAGE_SIZE}`);

      if (!response.ok) {
        throw new Error(`Could not load imports (${response.status})`);
      }

      const data = await response.json();
      this.setState({
        imports: data.items || [],
        pageNumber: data.pageNumber || 1,
        totalPages: data.totalPages || 0,
        totalCount: data.totalCount || 0,
        loading: false
      });
    } catch (error) {
      this.setState({ error: error.message, loading: false });
    }
  }

  openImport = async (id) => {
    this.setState({ detailsOpen: true, loadingDetails: true, details: null, filter: 'all' });

    try {
      const response = await fetch(`/api/${currentLanguage()}/imports/${id}`);

      if (!response.ok) {
        throw new Error(`Could not load import ${id} (${response.status})`);
      }

      this.setState({ details: await response.json(), loadingDetails: false });
    } catch (error) {
      this.setState({ loadingDetails: false, details: { error: error.message } });
    }
  }

  closeImport = () => {
    this.setState({ detailsOpen: false, details: null });

    if (window.location.search.includes('open=')) {
      window.history.replaceState(null, '', window.location.pathname);
    }
  }

  renderTable() {
    const { imports } = this.state;

    if (imports.length === 0) {
      return (
        <Alert color="info" data-testid="imports-empty">
          No imports yet for this language. Words come in from the{' '}
          <Link to="/bulk-import">Bulk</Link>, <Link to="/kindle-import">Kindle</Link>,{' '}
          <Link to="/lingq-import">LingQ</Link> and <Link to="/notes-import">Notes</Link> import pages.
        </Alert>
      );
    }

    return (
      <Table striped hover responsive data-testid="imports-table">
        <thead>
          <tr>
            <th>Date</th>
            <th>Source</th>
            <th>Name</th>
            <th>Tags</th>
            <th className="text-end">New</th>
            <th className="text-end">Already known</th>
            <th className="text-end">Skipped</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {imports.map(item => (
            <tr key={item.id} data-testid="import-row">
              <td><small>{formatDateTime(item.startedAtUtc)}</small></td>
              <td><Badge color="secondary">{kindLabel(item.kind)}</Badge></td>
              <td>
                {item.name || <span className="text-muted">{item.fileName || 'Untitled'}</span>}
                {item.isReconstructed && (
                  <Badge color="light" className="text-dark border ms-2" title="Put back together from encounters recorded before imports were tracked">
                    reconstructed
                  </Badge>
                )}
              </td>
              <td>
                {item.tags.map(tag => (
                  <Badge key={tag} color="info" className="me-1">{tag}</Badge>
                ))}
              </td>
              <td className="text-end" data-testid="import-words-created">{item.wordsCreated}</td>
              <td className="text-end">{item.wordsTouched - item.wordsCreated}</td>
              <td className="text-end">{item.termsSkipped}</td>
              <td>
                <Badge color={statusColor(item.status)} title={item.error || ''}>{item.status}</Badge>
              </td>
              <td>
                <Button size="sm" color="info" onClick={() => this.openImport(item.id)} data-testid="view-import">
                  View
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </Table>
    );
  }

  renderPagination() {
    const { pageNumber, totalPages, totalCount } = this.state;

    if (totalPages <= 1) {
      return null;
    }

    return (
      <div className="d-flex justify-content-between align-items-center">
        <small className="text-muted">{totalCount} imports</small>
        <ButtonGroup size="sm">
          <Button disabled={pageNumber <= 1} onClick={() => this.loadImports(pageNumber - 1)}>Previous</Button>
          <Button disabled>{pageNumber} / {totalPages}</Button>
          <Button disabled={pageNumber >= totalPages} onClick={() => this.loadImports(pageNumber + 1)}>Next</Button>
        </ButtonGroup>
      </div>
    );
  }

  renderDetails() {
    const { details, loadingDetails, filter } = this.state;

    if (loadingDetails || !details) {
      return <p><em>Loading...</em></p>;
    }

    if (details.error) {
      return <Alert color="danger">{details.error}</Alert>;
    }

    const words = details.items.filter(i => i.outcome !== 'Skipped');
    const skipped = details.items.filter(i => i.outcome === 'Skipped');
    const shown = words.filter(i =>
      filter === 'all' ||
      (filter === 'new' && i.outcome === 'Created') ||
      (filter === 'existing' && i.outcome === 'Existing'));

    return (
      <div data-testid="import-details">
        <div className="row mb-3">
          <div className="col-md-4">
            <strong>Source:</strong> {kindLabel(details.kind)}
            {details.fileName && <div><strong>File:</strong> {details.fileName}</div>}
          </div>
          <div className="col-md-4">
            <strong>Started:</strong> {formatDateTime(details.startedAtUtc)}
            {details.completedAtUtc && <div><strong>Finished:</strong> {formatDateTime(details.completedAtUtc)}</div>}
          </div>
          <div className="col-md-4">
            <strong>Status:</strong> <Badge color={statusColor(details.status)}>{details.status}</Badge>
            {details.tags.length > 0 && (
              <div>
                <strong>Tags:</strong>{' '}
                {details.tags.map(tag => <Badge key={tag} color="info" className="me-1">{tag}</Badge>)}
              </div>
            )}
          </div>
        </div>

        {details.error && <Alert color="danger">{details.error}</Alert>}

        {details.isReconstructed && (
          <Alert color="light" className="border">
            Reconstructed from encounters recorded before imports were tracked. Its grouping and
            dates are a best guess, and it has no record of terms it skipped.
          </Alert>
        )}

        <p>
          <strong>{details.termsRead}</strong> terms read · <strong>{details.wordsCreated}</strong> new words ·{' '}
          <strong>{details.wordsTouched - details.wordsCreated}</strong> already known ·{' '}
          <strong>{details.encountersCreated}</strong> encounters · <strong>{details.termsSkipped}</strong> skipped
          {details.externalCalls.calls > 0 && (
            <>
              {' '}· <Link to={`/history?tab=calls&importId=${details.id}`}>
                {details.externalCalls.calls} model/dictionary calls
              </Link>
              {details.externalCalls.promptTokens + details.externalCalls.completionTokens > 0 && (
                <> ({details.externalCalls.promptTokens + details.externalCalls.completionTokens} tokens)</>
              )}
            </>
          )}
        </p>

        {(details.examplesAdded > 0 || details.contentReopened > 0) && (
          <p className="text-muted" data-testid="import-study-effects">
            {details.examplesAdded > 0 && (
              <>{details.examplesAdded} sentence{details.examplesAdded === 1 ? '' : 's'} kept as practice examples. </>
            )}
            {details.contentReopened > 0 && (
              <>
                {details.contentReopened} word{details.contentReopened === 1 ? ' was' : 's were'} met in a new form, so
                {details.contentReopened === 1 ? ' its' : ' their'} study content will be generated again
                {details.externalCalls.laterStudyContentCalls > 0 && (
                  <> - {details.externalCalls.laterStudyContentCalls} of those calls made so far</>
                )}.
              </>
            )}
          </p>
        )}

        <div className="d-flex justify-content-between align-items-center mb-2">
          <h5 className="mb-0">Words ({words.length})</h5>
          <ButtonGroup size="sm">
            <Button outline={filter !== 'all'} color="secondary" onClick={() => this.setState({ filter: 'all' })}>All</Button>
            <Button outline={filter !== 'new'} color="success" onClick={() => this.setState({ filter: 'new' })} data-testid="filter-new">New</Button>
            <Button outline={filter !== 'existing'} color="secondary" onClick={() => this.setState({ filter: 'existing' })}>Already known</Button>
          </ButtonGroup>
        </div>

        <div style={{ maxHeight: '28rem', overflowY: 'auto' }}>
          <Table size="sm" striped data-testid="import-words">
            <thead>
              <tr>
                <th>Word</th>
                <th>As written</th>
                <th>Form</th>
                <th></th>
                <th>Encounter</th>
              </tr>
            </thead>
            <tbody>
              {shown.map(item => (
                <tr key={item.id} data-testid="import-word">
                  <td>
                    {item.wordId
                      ? <Link to={`/words?details=${item.wordId}`}>{item.headword}</Link>
                      : <span className="text-muted text-decoration-line-through" title="Deleted since">{item.headword}</span>}
                  </td>
                  <td className="text-muted">{item.sourceTerm !== item.headword ? item.sourceTerm : ''}</td>
                  <td className="text-muted">{item.form && item.form !== item.headword ? item.form : ''}</td>
                  <td>
                    {item.outcome === 'Created'
                      ? <Badge color="success">New</Badge>
                      : <Badge color="secondary">Already known</Badge>}
                    {item.exampleAdded && <Badge color="info" className="ms-1" title="The sentence it was met in became a practice example">example</Badge>}
                    {item.contentReopened && <Badge color="warning" className="ms-1" title="A new form - study content will be generated again">new form</Badge>}
                  </td>
                  <td>{item.encounterAdded ? 'added' : <span className="text-muted">already recorded</span>}</td>
                </tr>
              ))}
            </tbody>
          </Table>
        </div>

        {skipped.length > 0 && (
          <>
            <h5 className="mt-3">Not imported ({skipped.length})</h5>
            <div style={{ maxHeight: '16rem', overflowY: 'auto' }}>
              <Table size="sm" borderless data-testid="import-skipped">
                <tbody>
                  {skipped.map(item => (
                    <tr key={item.id}>
                      <td style={{ fontFamily: 'monospace' }}>{item.sourceTerm}</td>
                      <td className="text-muted">{item.reason}</td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            </div>
          </>
        )}
      </div>
    );
  }

  render() {
    const { loading, error, detailsOpen, details } = this.state;

    return (
      <div>
        <h1>Imports</h1>
        <p className="text-muted">
          Every import run for this language, and the words each brought in.
        </p>

        {error && <Alert color="danger">{error}</Alert>}
        {loading ? <p><em>Loading...</em></p> : this.renderTable()}
        {this.renderPagination()}

        <Modal isOpen={detailsOpen} toggle={this.closeImport} size="xl">
          <ModalHeader toggle={this.closeImport}>
            {details && !details.error
              ? <>{kindLabel(details.kind)} import{details.name ? `: ${details.name}` : ''}</>
              : 'Import'}
          </ModalHeader>
          <ModalBody>{detailsOpen && this.renderDetails()}</ModalBody>
          <ModalFooter>
            <Button color="secondary" onClick={this.closeImport}>Close</Button>
          </ModalFooter>
        </Modal>
      </div>
    );
  }
}
