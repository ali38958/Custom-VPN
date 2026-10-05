#!/bin/bash
sudo ip link set dev wg0 mtu 1420
sudo sed -i '/\[Interface\]/a MTU = 1420' /etc/wireguard/wg0.conf
grep -A 5 '\[Interface\]' /etc/wireguard/wg0.conf
ip addr show wg0 | grep mtu
