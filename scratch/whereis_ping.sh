#!/bin/bash
which ping || find /usr -name ping 2>/dev/null || find /bin -name ping 2>/dev/null
/bin/ping -c 2 10.77.0.1 2>/dev/null || ping -c 2 10.77.0.1
