fetch('https://resolvia.cc.cd/api/client/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'vpn_001',
    password: 'qwerty123',
    deviceId: 'test-device'
  })
}).then(r => r.json()).then(console.log).catch(console.error);
