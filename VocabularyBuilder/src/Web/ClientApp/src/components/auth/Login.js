import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import { Button, Form, FormGroup, Label, Input, Alert, Card, CardBody, CardHeader, Spinner } from 'reactstrap';
import { AuthContext } from './AuthContext';

export class Login extends Component {
  static displayName = Login.name;
  static contextType = AuthContext;

  constructor(props) {
    super(props);
    this.state = {
      email: '',
      password: '',
      remember: true,
      submitting: false,
      error: null
    };
  }

  handleInputChange = (e) => {
    const { name, type, checked, value } = e.target;
    this.setState({ [name]: type === 'checkbox' ? checked : value });
  }

  handleSubmit = async (e) => {
    e.preventDefault();

    const { email, password, remember } = this.state;

    this.setState({ submitting: true, error: null });

    try {
      // Signing in updates the context, and the route then sends the page on to where the
      // user was going - nothing more to do here once it succeeds
      await this.context.login(email.trim(), password, remember);
    } catch (error) {
      this.setState({ submitting: false, error: error.message });
    }
  }

  render() {
    const { email, password, remember, submitting, error } = this.state;

    return (
      <div className="row justify-content-center">
        <div className="col-md-6 col-lg-5">
          <Card>
            <CardHeader>
              <h1 className="h4 mb-0">Sign in</h1>
            </CardHeader>
            <CardBody>
              {error && <Alert color="danger">{error}</Alert>}

              <Form onSubmit={this.handleSubmit}>
                <FormGroup>
                  <Label for="email">Email</Label>
                  <Input
                    id="email"
                    name="email"
                    type="email"
                    autoComplete="username"
                    value={email}
                    onChange={this.handleInputChange}
                    required
                    autoFocus
                  />
                </FormGroup>
                <FormGroup>
                  <Label for="password">Password</Label>
                  <Input
                    id="password"
                    name="password"
                    type="password"
                    autoComplete="current-password"
                    value={password}
                    onChange={this.handleInputChange}
                    required
                  />
                </FormGroup>
                <FormGroup check className="mb-3">
                  <Input
                    id="remember"
                    name="remember"
                    type="checkbox"
                    checked={remember}
                    onChange={this.handleInputChange}
                  />
                  <Label check for="remember">Keep me signed in</Label>
                </FormGroup>
                <Button color="primary" type="submit" disabled={submitting}>
                  {submitting ? <><Spinner size="sm" /> Signing in...</> : 'Sign in'}
                </Button>
              </Form>

              <p className="mt-3 mb-0 text-muted">
                Invited to try it? <Link to="/register">Create an account</Link>
              </p>
            </CardBody>
          </Card>
        </div>
      </div>
    );
  }
}
