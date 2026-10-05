UPDATE users SET passwordHash = (SELECT passwordHash FROM users WHERE username = 'vpn_003') WHERE username = 'vpn_001';
