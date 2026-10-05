#!/bin/bash
sudo apt update && sudo apt install -y iputils-ping
ping -c 3 10.77.0.2
