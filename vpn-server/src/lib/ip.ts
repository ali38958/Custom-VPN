/**
 * Utility functions for converting and allocating IPv4 addresses
 * Default virtual subnet: 10.77.0.0/22
 * Range: 10.77.0.1 (Server) to 10.77.3.254 (Peers)
 */

export function ipToInt(ip: string): number {
  return ip
    .split(".")
    .reduce((acc, octet) => ((acc << 8) + parseInt(octet, 10)) >>> 0, 0);
}

export function intToIp(int: number): string {
  return [
    (int >>> 24) & 255,
    (int >>> 16) & 255,
    (int >>> 8) & 255,
    int & 255,
  ].join(".");
}

// 10.77.0.0/22 starts at 10.77.0.1, ends at 10.77.3.254
export const SUBNET_BASE = ipToInt("10.77.0.0");
export const SERVER_IP = ipToInt("10.77.0.1");
export const CLIENT_MIN_IP = ipToInt("10.77.0.2");
export const CLIENT_MAX_IP = ipToInt("10.77.3.254");

/**
 * Finds the lowest unallocated IP address within the subnet range
 */
export function findNextAvailableIp(allocatedIps: number[]): number | null {
  const set = new Set(allocatedIps);
  // Ensure server IP is marked as used
  set.add(SERVER_IP);

  for (let ip = CLIENT_MIN_IP; ip <= CLIENT_MAX_IP; ip++) {
    if (!set.has(ip)) {
      return ip;
    }
  }
  return null;
}
