#!/bin/bash
ip addr show wg0
ip route show
sudo iptables -L INPUT -n -v --line-numbers | head -n 10
