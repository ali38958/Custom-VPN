USE privatenet;
UPDATE users SET assigned_ip = REPLACE(assigned_ip, '10.77.0.', '10.8.0.');
