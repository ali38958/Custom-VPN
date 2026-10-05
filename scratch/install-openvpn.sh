#!/bin/bash
export DEBIAN_FRONTEND=noninteractive
sudo apt-get update
sudo apt-get install -y openvpn easy-rsa

# Setup PKI
make-cadir ~/openvpn-ca
cd ~/openvpn-ca
cat << 'EASY' > vars
set_var EASYRSA_REQ_COUNTRY    "US"
set_var EASYRSA_REQ_PROVINCE   "CA"
set_var EASYRSA_REQ_CITY       "SanFrancisco"
set_var EASYRSA_REQ_ORG        "MyVPN"
set_var EASYRSA_REQ_EMAIL      "admin@myvpn.local"
set_var EASYRSA_REQ_OU         "MyVPN"
set_var EASYRSA_BATCH          "1"
EASY
./easyrsa init-pki
./easyrsa build-ca nopass
./easyrsa gen-req server nopass
./easyrsa sign-req server server
./easyrsa gen-dh
openvpn --genkey secret ta.key

sudo cp pki/ca.crt pki/issued/server.crt pki/private/server.key pki/dh.pem ta.key /etc/openvpn/server/

# Setup server.conf
cat << 'CONF' | sudo tee /etc/openvpn/server/server.conf
port 443
proto tcp
dev tun
ca ca.crt
cert server.crt
key server.key
dh dh.pem
tls-auth ta.key 0
server 10.8.0.0 255.255.255.0
ifconfig-pool-persist ipp.txt
push "redirect-gateway def1 bypass-dhcp"
push "dhcp-option DNS 8.8.8.8"
push "dhcp-option DNS 1.1.1.1"
keepalive 10 120
cipher AES-256-CBC
user nobody
group nogroup
persist-key
persist-tun
status openvpn-status.log
verb 3
CONF

# Setup iptables (assuming enp0s6 is the main interface)
sudo iptables -t nat -A POSTROUTING -s 10.8.0.0/24 -o enp0s6 -j MASQUERADE
sudo iptables -A FORWARD -i tun0 -j ACCEPT
sudo iptables -A FORWARD -o tun0 -j ACCEPT
sudo netfilter-persistent save

sudo systemctl enable openvpn-server@server
sudo systemctl start openvpn-server@server
