#!/bin/bash
cat << EOF > ~/openvpn-ca/client1.ovpn
client
dev tun
proto tcp
remote 144.24.25.135 443
resolv-retry infinite
nobind
persist-key
persist-tun
remote-cert-tls server
cipher AES-256-CBC
verb 3
<ca>
$(cat ~/openvpn-ca/pki/ca.crt)
</ca>
<cert>
$(awk '/-----BEGIN CERTIFICATE-----/{flag=1} flag; /-----END CERTIFICATE-----/{flag=0}' ~/openvpn-ca/pki/issued/client1.crt)
</cert>
<key>
$(cat ~/openvpn-ca/pki/private/client1.key)
</key>
<tls-auth>
$(cat ~/openvpn-ca/ta.key)
</tls-auth>
key-direction 1
EOF
cat ~/openvpn-ca/client1.ovpn
