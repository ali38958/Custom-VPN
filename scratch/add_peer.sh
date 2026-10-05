#!/bin/bash
sudo wg set wg0 peer "7O5NouBSYoMBSMlGcOrY4mcGk8PVIMef+mfu24PIR1E=" allowed-ips "10.77.0.2/32"
sudo wg show
