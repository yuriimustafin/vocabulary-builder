import React, { Component } from 'react';
import { Alert, Badge, Button, ButtonGroup, FormGroup, Input, Label, Modal, ModalBody, ModalFooter, ModalHeader, Nav, NavItem, NavLink, Table } from 'reactstrap';
import { Link } from 'react-router-dom';
import { currentLanguage, formatDateTime, humanize } from './historyFormat';

const PAGE_SIZE = 50;

const CATEGORIES = ['Words', 'Dictionary', 'Imports', 'Study', 'StudyContent', 'Lists'];
const CATEGORY_COLORS = {
  Words: 'primary', Dictionary: 'info', Imports: 'success', Study: 'warning', StudyContent: 'dark', Lists: 'secondary'
};

const TABS = ['activity', 'reviews', 'calls', 'usage'];

const GRADE_COLORS = { Again: 'danger', Hard: 'warning', Good: 'success', Easy: 'primary' };

const PROVIDERS = ['Gpt', 'WordReference', 'Oxford'];
const PURPOSES = ['DictionaryEntry', 'Conjugation', 'StudyContent', 'NotesExtraction', 'LemmaResolution', 'ListGeneration', 'AudioText', 'Other'];

function queryParams() {
  const params = new URLSearchParams(window.location.search);
  const number = (key) => (params.get(key) ? parseInt(params.get(key), 10) : null);

  return {
    tab: TABS.includes(params.get('tab')) ? params.get('tab') : 'activity',
    wordId: number('wordId'),
    importId: number('importId')
  };
}

/**
 * The history of the signed-in user's data: what was done to it, every answer given in study,
 * and every request made to a model or a dictionary site on their behalf - with what those
 * requests added up to.
 *
 * ?tab=activity|reviews|calls|usage, ?wordId= and ?importId= narrow it, which is how the word
 * details and the import popup link here.
 */
export class History extends Component {
  static displayName = History.name;

  constructor(props) {
    super(props);
    const { tab, wordId, importId } = queryParams();

    this.state = {
      tab,
      wordId,
      importId,
      category: '',
      provider: '',
      purpose: '',
      failedOnly: false,
      includeFollowUps: false,
      usageDays: '30',
      rows: [],
      pageNumber: 1,
      totalPages: 0,
      totalCount: 0,
      loading: true,
      error: null,
      expanded: null,
      call: null,
      callOpen: false
    };
  }

  componentDidMount() {
    this.load(1);
  }

  setTab = (tab) => {
    this.setState({ tab, rows: [], expanded: null }, () => this.load(1));
  }

  setFilter = (changes) => {
    this.setState(changes, () => this.load(1));
  }

  clearScope = () => {
    window.history.replaceState(null, '', `${window.location.pathname}?tab=${this.state.tab}`);
    this.setFilter({ wordId: null, importId: null });
  }

  async load(pageNumber) {
    const { tab, category, provider, purpose, failedOnly, includeFollowUps, usageDays, wordId, importId } = this.state;
    const params = new URLSearchParams({ pageNumber, pageSize: PAGE_SIZE });

    if (wordId) params.set('wordId', wordId);
    if (importId && tab !== 'reviews') params.set('importId', importId);

    if (tab === 'activity') {
      if (category) params.set('category', category);
    } else if (tab === 'calls') {
      if (provider) params.set('provider', provider);
      if (purpose) params.set('purpose', purpose);
      if (failedOnly) params.set('failedOnly', 'true');
    } else if (tab === 'reviews') {
      if (includeFollowUps) params.set('includeFollowUps', 'true');
    }

    this.setState({ loading: true, error: null });

    try {
      const url = tab === 'usage'
        ? `/api/${currentLanguage()}/history/usage${usageDays ? `?days=${usageDays}` : ''}`
        : `/api/${currentLanguage()}/history/${tab}?${params}`;
      const response = await fetch(url);

      if (!response.ok) {
        throw new Error(`Could not load the history (${response.status})`);
      }

      const data = await response.json();

      if (tab === 'usage') {
        this.setState({ rows: data, pageNumber: 1, totalPages: 0, totalCount: data.length, loading: false });
        return;
      }

      this.setState({
        rows: data.items || [],
        pageNumber: data.pageNumber || 1,
        totalPages: data.totalPages || 0,
        totalCount: data.totalCount || 0,
        loading: false
      });
    } catch (error) {
      this.setState({ error: error.message, loading: false });
    }
  }

  openCall = async (id) => {
    this.setState({ callOpen: true, call: null });

    try {
      const response = await fetch(`/api/${currentLanguage()}/history/calls/${id}`);
      this.setState({ call: response.ok ? await response.json() : { error: `Could not load call ${id}` } });
    } catch (error) {
      this.setState({ call: { error: error.message } });
    }
  }

  renderFilters() {
    const { tab, category, provider, purpose, failedOnly, includeFollowUps, usageDays, wordId, importId } = this.state;

    return (
      <div className="d-flex flex-wrap gap-3 align-items-end mb-3">
        {tab === 'reviews' && (
          <FormGroup check className="mb-1">
            <Input id="history-follow-ups" type="checkbox" checked={includeFollowUps}
              onChange={e => this.setFilter({ includeFollowUps: e.target.checked })} />
            <Label for="history-follow-ups" check className="small">Include unscored follow-ups</Label>
          </FormGroup>
        )}
        {tab === 'usage' && (
          <FormGroup className="mb-0">
            <Label for="history-days" className="small mb-1">Period</Label>
            <Input id="history-days" type="select" bsSize="sm" value={usageDays}
              onChange={e => this.setFilter({ usageDays: e.target.value })} data-testid="usage-days">
              <option value="1">Last day</option>
              <option value="7">Last 7 days</option>
              <option value="30">Last 30 days</option>
              <option value="">All time</option>
            </Input>
          </FormGroup>
        )}
        {(tab === 'reviews' || tab === 'usage') ? null : tab === 'activity' ? (
          <FormGroup className="mb-0">
            <Label for="history-category" className="small mb-1">Category</Label>
            <Input id="history-category" type="select" bsSize="sm" value={category}
              onChange={e => this.setFilter({ category: e.target.value })} data-testid="history-category">
              <option value="">All</option>
              {CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
            </Input>
          </FormGroup>
        ) : (
          <>
            <FormGroup className="mb-0">
              <Label for="history-provider" className="small mb-1">Service</Label>
              <Input id="history-provider" type="select" bsSize="sm" value={provider}
                onChange={e => this.setFilter({ provider: e.target.value })}>
                <option value="">All</option>
                {PROVIDERS.map(p => <option key={p} value={p}>{p}</option>)}
              </Input>
            </FormGroup>
            <FormGroup className="mb-0">
              <Label for="history-purpose" className="small mb-1">Purpose</Label>
              <Input id="history-purpose" type="select" bsSize="sm" value={purpose}
                onChange={e => this.setFilter({ purpose: e.target.value })}>
                <option value="">All</option>
                {PURPOSES.map(p => <option key={p} value={p}>{humanize(p)}</option>)}
              </Input>
            </FormGroup>
            <FormGroup check className="mb-1">
              <Input id="history-failed" type="checkbox" checked={failedOnly}
                onChange={e => this.setFilter({ failedOnly: e.target.checked })} />
              <Label for="history-failed" check className="small">Failed only</Label>
            </FormGroup>
          </>
        )}

        {tab !== 'usage' && (wordId || (importId && tab !== 'reviews')) && (
          <div className="mb-1">
            <Badge color="dark" className="me-2">
              {wordId ? `Word #${wordId}` : `Import #${importId}`}
            </Badge>
            <Button size="sm" color="link" className="p-0" onClick={this.clearScope}>show everything</Button>
          </div>
        )}
      </div>
    );
  }

  renderActivity() {
    const { rows, expanded } = this.state;

    if (rows.length === 0) {
      return <Alert color="info">Nothing recorded yet.</Alert>;
    }

    return (
      <Table striped hover responsive size="sm" data-testid="activity-table">
        <thead>
          <tr>
            <th>When</th>
            <th>Category</th>
            <th>Action</th>
            <th>Word</th>
            <th>What happened</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {rows.map(row => (
            <React.Fragment key={row.id}>
              <tr data-testid="activity-row">
                <td><small>{formatDateTime(row.occurredAtUtc)}</small></td>
                <td><Badge color={CATEGORY_COLORS[row.category] || 'secondary'}>{row.category}</Badge></td>
                <td data-testid="activity-action">{humanize(row.action)}</td>
                <td>
                  {row.wordId && row.action !== 'WordDeleted'
                    ? <Link to={`/words?details=${row.wordId}`}>{row.headword}</Link>
                    : row.headword || ''}
                </td>
                <td>
                  {row.summary}
                  {row.importId && (
                    <> · <Link to={`/imports?open=${row.importId}`}>view import</Link></>
                  )}
                </td>
                <td>
                  {row.details && (
                    <Button size="sm" color="link" className="p-0"
                      onClick={() => this.setState({ expanded: expanded === row.id ? null : row.id })}>
                      {expanded === row.id ? 'hide' : 'details'}
                    </Button>
                  )}
                </td>
              </tr>
              {expanded === row.id && (
                <tr>
                  <td colSpan={6}>
                    <pre className="mb-0 small" style={{ whiteSpace: 'pre-wrap' }}>
                      {JSON.stringify(JSON.parse(row.details), null, 2)}
                    </pre>
                  </td>
                </tr>
              )}
            </React.Fragment>
          ))}
        </tbody>
      </Table>
    );
  }

  renderReviews() {
    const { rows } = this.state;

    if (rows.length === 0) {
      return <Alert color="info">No answers recorded yet.</Alert>;
    }

    return (
      <Table striped hover responsive size="sm" data-testid="reviews-table">
        <thead>
          <tr>
            <th>When</th>
            <th>Word</th>
            <th>Exercise</th>
            <th>Answer</th>
            <th>Grade</th>
            <th className="text-end">Level</th>
            <th className="text-end">Interval</th>
            <th className="text-end">Time</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(row => (
            <tr key={row.id} data-testid="review-row" className={row.voidedAtUtc ? 'text-muted' : ''}>
              <td><small>{formatDateTime(row.reviewedAtUtc)}</small></td>
              <td><Link to={`/words?details=${row.wordId}`}>{row.headword}</Link></td>
              <td>
                {humanize(row.exerciseType)}
                {row.isScaffold && <Badge color="light" className="text-dark border ms-1">follow-up</Badge>}
                {row.exampleSentence && <div><small className="text-muted">{row.exampleSentence}</small></div>}
              </td>
              <td>
                {row.answer && <span style={{ fontFamily: 'monospace' }}>{row.answer}</span>}
                {row.answerMatch && row.answerMatch !== 'Exact' && (
                  <Badge color="light" className="text-dark border ms-1">{humanize(row.answerMatch)}</Badge>
                )}
              </td>
              <td>
                <Badge color={GRADE_COLORS[row.grade] || 'secondary'}>{row.grade}</Badge>
                {row.hintUsed && <Badge color="info" className="ms-1">hint</Badge>}
                {row.tolerated && <Badge color="light" className="text-dark border ms-1" title="A miss that cost the word nothing">tolerated</Badge>}
                {row.voidedAtUtc && (
                  <Badge color="secondary" className="ms-1" title={`Voided ${formatDateTime(row.voidedAtUtc)}`}>
                    {humanize(row.voidReason || 'Voided')}
                  </Badge>
                )}
              </td>
              <td className="text-end"><small>{row.rungBefore} → {row.rungAfter}</small></td>
              <td className="text-end"><small>{row.intervalBeforeDays} → {row.intervalAfterDays} d</small></td>
              <td className="text-end"><small>{(row.elapsedMs / 1000).toFixed(1)} s</small></td>
            </tr>
          ))}
        </tbody>
      </Table>
    );
  }

  renderUsage() {
    const { rows } = this.state;

    if (rows.length === 0) {
      return <Alert color="info">No calls in this period.</Alert>;
    }

    const total = (field) => rows.reduce((sum, row) => sum + row[field], 0);

    return (
      <Table striped hover responsive size="sm" data-testid="usage-table">
        <thead>
          <tr>
            <th>Service</th>
            <th>Purpose</th>
            <th>Prompt version</th>
            <th className="text-end">Calls</th>
            <th className="text-end">Failed</th>
            <th className="text-end">Prompt tokens</th>
            <th className="text-end">Completion tokens</th>
            <th className="text-end">Avg time</th>
            <th>Period</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(row => (
            <tr key={`${row.provider}-${row.purpose}-${row.promptVersion}`} data-testid="usage-row">
              <td><Badge color="secondary">{row.provider}</Badge></td>
              <td>{humanize(row.purpose)}</td>
              <td>{row.promptVersion || ''}</td>
              <td className="text-end">{row.calls}</td>
              <td className="text-end">{row.failed || ''}</td>
              <td className="text-end">{row.promptTokens.toLocaleString()}</td>
              <td className="text-end">{row.completionTokens.toLocaleString()}</td>
              <td className="text-end"><small>{Math.round(row.totalDurationMs / row.calls)} ms</small></td>
              <td><small>{formatDateTime(row.firstAtUtc)} – {formatDateTime(row.lastAtUtc)}</small></td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="fw-bold">
            <td colSpan={3}>Total</td>
            <td className="text-end">{total('calls')}</td>
            <td className="text-end">{total('failed') || ''}</td>
            <td className="text-end">{total('promptTokens').toLocaleString()}</td>
            <td className="text-end">{total('completionTokens').toLocaleString()}</td>
            <td colSpan={2}></td>
          </tr>
        </tfoot>
      </Table>
    );
  }

  renderTab() {
    switch (this.state.tab) {
      case 'reviews': return this.renderReviews();
      case 'calls': return this.renderCalls();
      case 'usage': return this.renderUsage();
      default: return this.renderActivity();
    }
  }

  renderCalls() {
    const { rows } = this.state;

    if (rows.length === 0) {
      return <Alert color="info">No calls recorded yet.</Alert>;
    }

    return (
      <Table striped hover responsive size="sm" data-testid="calls-table">
        <thead>
          <tr>
            <th>When</th>
            <th>Service</th>
            <th>Purpose</th>
            <th>About</th>
            <th>Result</th>
            <th className="text-end">Time</th>
            <th className="text-end">Tokens</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {rows.map(row => (
            <tr key={row.id} data-testid="call-row">
              <td><small>{formatDateTime(row.startedAtUtc)}</small></td>
              <td>
                <Badge color="secondary">{row.provider}</Badge>
                {row.isMock && <Badge color="light" className="text-dark border ms-1">mock</Badge>}
              </td>
              <td>{humanize(row.purpose)}</td>
              <td>
                {row.wordId ? <Link to={`/words?details=${row.wordId}`}>{row.target}</Link> : row.target}
                {row.importId && <> · <Link to={`/imports?open=${row.importId}`}>import</Link></>}
              </td>
              <td>
                {row.succeeded
                  ? <Badge color="success">{row.statusCode || 'OK'}</Badge>
                  : <Badge color="danger" title={row.error || ''}>{row.statusCode || 'failed'}</Badge>}
                {!row.succeeded && row.error && <small className="text-muted ms-1">{row.error}</small>}
              </td>
              <td className="text-end"><small>{row.durationMs} ms</small></td>
              <td className="text-end">
                <small>{row.promptTokens != null ? `${row.promptTokens} + ${row.completionTokens ?? 0}` : ''}</small>
              </td>
              <td>
                {row.hasBody && (
                  <Button size="sm" color="info" onClick={() => this.openCall(row.id)} data-testid="view-call">View</Button>
                )}
                {!row.hasBody && row.url && (
                  <a href={row.url} target="_blank" rel="noopener noreferrer" className="small">page</a>
                )}
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
        <small className="text-muted">{totalCount} entries</small>
        <ButtonGroup size="sm">
          <Button disabled={pageNumber <= 1} onClick={() => this.load(pageNumber - 1)}>Previous</Button>
          <Button disabled>{pageNumber} / {totalPages}</Button>
          <Button disabled={pageNumber >= totalPages} onClick={() => this.load(pageNumber + 1)}>Next</Button>
        </ButtonGroup>
      </div>
    );
  }

  renderCall() {
    const { call } = this.state;

    if (!call) {
      return <p><em>Loading...</em></p>;
    }

    if (call.error && !call.id) {
      return <Alert color="danger">{call.error}</Alert>;
    }

    return (
      <div data-testid="call-details">
        <p className="mb-2">
          <Badge color="secondary">{call.provider}</Badge> {humanize(call.purpose)}
          {call.target && <> · {call.target}</>}
          {call.model && <> · {call.model}</>}
          {' '}· {formatDateTime(call.startedAtUtc)} · {call.durationMs} ms
          {call.promptTokens != null && <> · {call.promptTokens} prompt + {call.completionTokens ?? 0} completion tokens</>}
        </p>
        {call.error && <Alert color="danger">{call.error}</Alert>}
        <h6>Prompt</h6>
        <pre className="border rounded p-2 small" style={{ whiteSpace: 'pre-wrap', maxHeight: '20rem', overflowY: 'auto' }}>
          {call.request}
        </pre>
        <h6>Response</h6>
        <pre className="border rounded p-2 small" style={{ whiteSpace: 'pre-wrap', maxHeight: '24rem', overflowY: 'auto' }}>
          {call.response || '(none)'}
        </pre>
      </div>
    );
  }

  render() {
    const { tab, loading, error, callOpen } = this.state;

    return (
      <div>
        <h1>History</h1>

        <Nav tabs className="mb-3">
          <NavItem>
            <NavLink href="#" active={tab === 'activity'} onClick={e => { e.preventDefault(); this.setTab('activity'); }}
              data-testid="tab-activity">
              Activity
            </NavLink>
          </NavItem>
          <NavItem>
            <NavLink href="#" active={tab === 'reviews'} onClick={e => { e.preventDefault(); this.setTab('reviews'); }}
              data-testid="tab-reviews">
              Study answers
            </NavLink>
          </NavItem>
          <NavItem>
            <NavLink href="#" active={tab === 'calls'} onClick={e => { e.preventDefault(); this.setTab('calls'); }}
              data-testid="tab-calls">
              Model &amp; dictionary calls
            </NavLink>
          </NavItem>
          <NavItem>
            <NavLink href="#" active={tab === 'usage'} onClick={e => { e.preventDefault(); this.setTab('usage'); }}
              data-testid="tab-usage">
              Usage
            </NavLink>
          </NavItem>
        </Nav>

        {this.renderFilters()}

        {error && <Alert color="danger">{error}</Alert>}
        {loading ? <p><em>Loading...</em></p> : this.renderTab()}
        {this.renderPagination()}

        <Modal isOpen={callOpen} toggle={() => this.setState({ callOpen: false })} size="xl">
          <ModalHeader toggle={() => this.setState({ callOpen: false })}>Call</ModalHeader>
          <ModalBody>{callOpen && this.renderCall()}</ModalBody>
          <ModalFooter>
            <Button color="secondary" onClick={() => this.setState({ callOpen: false })}>Close</Button>
          </ModalFooter>
        </Modal>
      </div>
    );
  }
}
