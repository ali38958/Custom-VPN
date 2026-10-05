UPDATE users SET password_hash = (SELECT password_hash FROM users WHERE username = 'vpn_003') WHERE username = 'vpn_001';
