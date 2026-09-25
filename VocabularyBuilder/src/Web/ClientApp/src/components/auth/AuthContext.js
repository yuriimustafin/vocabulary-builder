import React, { Component } from 'react';

/**
 * Who is signed in, and the means to change that.
 *
 * The session itself is an HttpOnly cookie that the browser sends with every same-origin
 * fetch, so nothing here stores a token and none of the existing pages had to change to
 * send one. What this adds is knowing whether there is a session at all.
 */
export const AuthContext = React.createContext({
  user: null,
  loading: true,
  login: async () => {},
  register: async () => {},
  logout: async () => {}
});

const AUTH_PATH = '/api/Users/';

let sessionWatchInstalled = false;

/**
 * Notices a session ending while the app is open - it expired, or was signed out in another
 * tab - from the 401 the next API call gets back.
 *
 * Wrapping fetch once here is what lets every page keep calling plain fetch rather than each
 * one learning to handle an expired session. The account endpoints are left out: a 401 from
 * /login is a wrong password, and one from /me is the ordinary answer when nobody is signed in.
 */
function watchForEndedSession(onEnded) {
  if (sessionWatchInstalled) {
    return;
  }
  sessionWatchInstalled = true;

  const fetchUnwatched = window.fetch.bind(window);

  window.fetch = async (input, init) => {
    const response = await fetchUnwatched(input, init);

    const url = new URL(typeof input === 'string' ? input : input.url, window.location.origin);
    const isApi = url.origin === window.location.origin && url.pathname.startsWith('/api/');

    if (response.status === 401 && isApi && !url.pathname.startsWith(AUTH_PATH)) {
      onEnded();
    }

    return response;
  };
}

/**
 * Turns an Identity error response into something to show under the form. Registration
 * answers with a validation problem keyed by error code; login with a bare status.
 */
export function describeProblem(problem, fallback) {
  if (problem && problem.errors) {
    return Object.values(problem.errors).flat().join(' ');
  }

  // The detail is SignInResult.ToString(), which spells one of them "Lockedout"
  switch (((problem && problem.detail) || '').toLowerCase()) {
    case 'failed':
      return 'That email and password do not match an account.';
    case 'lockedout':
      return 'Too many failed attempts. Try again in a few minutes.';
    case 'notallowed':
      return 'This account is not allowed to sign in yet.';
    default:
      return fallback;
  }
}

async function readProblem(response) {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

export class AuthProvider extends Component {
  static displayName = AuthProvider.name;

  constructor(props) {
    super(props);

    this.state = {
      user: null,
      loading: true,
      login: this.login,
      register: this.register,
      logout: this.logout
    };
  }

  componentDidMount() {
    watchForEndedSession(() => this.setState({ user: null }));
    this.refresh();
  }

  refresh = async () => {
    try {
      const response = await fetch(`${AUTH_PATH}me`);
      this.setState({ user: response.ok ? await response.json() : null, loading: false });
    } catch (error) {
      console.error('Could not find out who is signed in:', error);
      this.setState({ user: null, loading: false });
    }
  }

  /**
   * @param {boolean} remember keep the session after the browser closes, rather than for
   * as long as it stays open
   */
  login = async (email, password, remember) => {
    const params = new URLSearchParams({ useCookies: 'true', useSessionCookies: remember ? 'false' : 'true' });

    const response = await fetch(`${AUTH_PATH}login?${params.toString()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (!response.ok) {
      throw new Error(describeProblem(await readProblem(response), `Sign-in failed (${response.status}).`));
    }

    await this.refresh();
  }

  register = async (email, password) => {
    const response = await fetch(`${AUTH_PATH}register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (!response.ok) {
      throw new Error(describeProblem(await readProblem(response), `Registration failed (${response.status}).`));
    }

    await this.login(email, password, true);
  }

  logout = async () => {
    try {
      await fetch(`${AUTH_PATH}logout`, { method: 'POST' });
    } finally {
      this.setState({ user: null });
    }
  }

  render() {
    return (
      <AuthContext.Provider value={this.state}>
        {this.props.children}
      </AuthContext.Provider>
    );
  }
}
