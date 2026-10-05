#!/bin/bash
sudo timeout 6 tcpdump -n -i wg0 icmp
