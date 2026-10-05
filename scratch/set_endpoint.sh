#!/bin/bash
sed -i 's#SERVER_ENDPOINT=.*#SERVER_ENDPOINT="144.24.25.135:51820"#' /var/www/Custom-VPN/vpn-server/.env
sudo systemctl restart customvpn.service
grep SERVER_ENDPOINT /var/www/Custom-VPN/vpn-server/.env
