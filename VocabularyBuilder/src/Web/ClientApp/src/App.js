import React, { Component } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { Spinner } from 'reactstrap';
import AppRoutes from './AppRoutes';
import { Layout } from './components/Layout';
import { AuthContext, AuthProvider } from './components/auth/AuthContext';
import { Login } from './components/auth/Login';
import { Register } from './components/auth/Register';
import './custom.css';

/**
 * Where to go once signed in: back to the page the user was sent away from - but only a page
 * of this app, so a crafted link cannot use the login form to send someone elsewhere.
 */
function returnUrl() {
  const target = new URLSearchParams(window.location.search).get('returnUrl');

  return target && target.startsWith('/') && !target.startsWith('//') ? target : '/';
}

function loginUrl() {
  const here = window.location.pathname + window.location.search;

  return here === '/' ? '/login' : `/login?returnUrl=${encodeURIComponent(here)}`;
}

export default class App extends Component {
  static displayName = App.name;

  renderRoutes = ({ user, loading }) => {
    if (loading) {
      return (
        <div className="text-center mt-5">
          <Spinner />
        </div>
      );
    }

    return (
      <Layout>
        <Routes>
          <Route path="/login" element={user ? <Navigate to={returnUrl()} replace /> : <Login />} />
          <Route path="/register" element={user ? <Navigate to="/" replace /> : <Register />} />
          {AppRoutes.map((route, index) => {
            const { element, ...rest } = route;
            // Every page needs someone signed in. The API would refuse them anyway; this
            // sends the browser to the login form instead of a page full of failed requests
            return <Route key={index} {...rest} element={user ? element : <Navigate to={loginUrl()} replace />} />;
          })}
        </Routes>
      </Layout>
    );
  }

  render() {
    return (
      <AuthProvider>
        <AuthContext.Consumer>
          {this.renderRoutes}
        </AuthContext.Consumer>
      </AuthProvider>
    );
  }
}
