// Wait for both backend and React dev server to be ready before running E2E tests
const https = require('https');

const checkServer = (url) => {
  return new Promise((resolve) => {
    const agent = new https.Agent({ rejectUnauthorized: false });
    https.get(url, { agent }, (res) => {
      resolve(res.statusCode >= 200 && res.statusCode < 500);
    }).on('error', () => {
      resolve(false);
    });
  });
};

const waitForServer = async (url, name, maxAttempts = 60) => {
  console.log(`Waiting for ${name} at ${url}...`);
  
  for (let i = 0; i < maxAttempts; i++) {
    const isReady = await checkServer(url);
    if (isReady) {
      console.log(`✓ ${name} is ready!`);
      return true;
    }
    await new Promise(resolve => setTimeout(resolve, 1000));
    if (i % 5 === 0 && i > 0) {
      console.log(`  Still waiting for ${name}... (${i}s)`);
    }
  }
  
  console.error(`✗ ${name} failed to start after ${maxAttempts}s`);
  return false;
};

(async () => {
  const backendReady = await waitForServer('https://localhost:5001', 'Backend API', 30);
  if (!backendReady) process.exit(1);
  
  const spaReady = await waitForServer('https://localhost:44447', 'React Dev Server', 60);
  if (!spaReady) process.exit(1);
  
  console.log('✓ All servers ready! Running tests...\n');
})();
