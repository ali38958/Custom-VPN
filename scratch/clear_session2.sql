DELETE FROM sessions WHERE user_id = (SELECT id FROM users WHERE username = 'vpn_001');
