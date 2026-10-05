#!/bin/bash
sed -i 's#SERVER_PUBLIC_KEY=.*#SERVER_PUBLIC_KEY="p+jZFDQaOQVcr84oqZiYDmamfTdID+3wUeH912gYLR8="#' /var/www/Custom-VPN/vpn-server/.env
sudo systemctl restart customvpn.service
echo "Updated SERVER_PUBLIC_KEY:"
grep SERVER_PUBLIC_KEY /var/www/Custom-VPN/vpn-server/.env
