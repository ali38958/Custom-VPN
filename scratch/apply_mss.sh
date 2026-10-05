#!/bin/bash
sudo iptables -t mangle -I FORWARD 1 -p tcp --tcp-flags SYN,RST SYN -j TCPMSS --clamp-mss-to-pmtu
ip link set dev wg0 mtu 1420
sudo iptables -t mangle -L FORWARD -n -v
ip addr show wg0
