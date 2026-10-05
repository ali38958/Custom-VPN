scp -i C:\sshkeys\ssh-key-2026-09-11.key -o StrictHostKeyChecking=no d:\Projects\MyVPN\scratch\hash.js ubuntu@144.24.25.135:/var/www/Custom-VPN/vpn-server/
ssh -i C:\sshkeys\ssh-key-2026-09-11.key -o StrictHostKeyChecking=no ubuntu@144.24.25.135 "cd /var/www/Custom-VPN/vpn-server && node hash.js"
