#!/bin/bash
sudo timeout 6 tcpdump -n -i enp0s6 udp port 51820
