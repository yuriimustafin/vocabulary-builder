import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import { Button, Form, FormGroup, FormText, Label, Input, Alert, Card, CardBody, CardHeader, Spinner } from 'reactstrap';
import { AuthContext } from './AuthContext';

export class Register extends Component {
  static displayName = Register.name;
  static contextType = AuthContext;

  constructor(props) {
    super(props);
    this.state = {
      email: '',
      password: '',
      confirmPassword: '',
      submitting: false,
      error: null
    };
  }

  handleInputChange = (e) => {
    this.setState({ [e.target.name]: e.target.value });
  }

  handleSubmit = async (e) => {
    e.preventDefault();

    const { email, password, confirmPassword } = this.state;

    if (password !== confirmPassword) {
      this.setState({ error: 'The passwords do not match.' });
      return;
    }

    this.setState({ submitting: true, error: null });

    try {
      // Registering signs the new account straight in, and the route moves on from there
      await this.context.register(email.trim(), password);
    } catch (error) {
      this.setState({ submitting: false, error: error.message });
    }
  }

  render() {
    const { email, password, confirmPassword, submitting, error } = this.state;

    return (
      <div className="row justify-content-center">
        <div className="col-md-6 col-lg-5">
          <Card>
            <CardHeader>
              <h1 className="h4 mb-0">Create an account</h1>
            </CardHeader>
            <CardBody>
              <p className="text-muted">
                Accounts are open to invited email addresses only.
              </p>

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
                    autoComplete="new-password"
                    value={password}
                    onChange={this.handleInputChange}
                    required
                  />
                  <FormText>
                    At least 6 characters, with an upper and a lower case letter, a digit and a symbol.
                  </FormText>
                </FormGroup>
                <FormGroup>
                  <Label for="confirmPassword">Confirm password</Label>
                  <Input
                    id="confirmPassword"
                    name="confirmPassword"
                    type="password"
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={this.handleInputChange}
                    required
                  />
                </FormGroup>
                <Button color="primary" type="submit" disabled={submitting}>
                  {submitting ? <><Spinner size="sm" /> Creating account...</> : 'Create account'}
                </Button>
              </Form>

              <p className="mt-3 mb-0 text-muted">
                Already have an account? <Link to="/login">Sign in</Link>
              </p>
            </CardBody>
          </Card>
        </div>
      </div>
    );
  }
}
