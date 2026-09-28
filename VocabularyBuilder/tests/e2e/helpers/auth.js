/**
 * The accounts the end-to-end suite signs in with.
 *
 * The administrator is created by the backend itself on startup, from the Admin section of
 * appsettings.E2ETest.json, and survives every database reset. The learners are on the
 * registration allowlist in the same file and are created by the specs that need them; the
 * reset removes them again.
 */
const path = require('path');

const ADMIN = {
  email: 'e2e@example.com',
  password: 'E2e-Passw0rd!'
};

const LEARNER = {
  email: 'learner@example.com',
  password: 'Learner-Passw0rd!'
};

/** Where the setup project saves the administrator's session for every spec to start from */
const AUTH_FILE = path.join(__dirname, '..', '..', '..', 'playwright', '.auth', 'e2e-admin.json');

/** For a spec that has to start signed out */
const SIGNED_OUT = { cookies: [], origins: [] };

module.exports = {
  ADMIN,
  LEARNER,
  AUTH_FILE,
  SIGNED_OUT
};
