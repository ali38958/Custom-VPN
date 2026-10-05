DELETE FROM session WHERE userId = (SELECT id FROM users WHERE username = 'vpn_001');
