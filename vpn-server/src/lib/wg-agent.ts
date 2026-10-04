import { exec } from "child_process";
import { promisify } from "util";

const execAsync = promisify(exec);

const WG_INTERFACE = process.env.WG_INTERFACE || "wg0";

/**
 * Adds or updates a peer in the live WireGuard interface immediately.
 */
export async function addPeerToWireGuard(publicKey: string, assignedIp: string): Promise<boolean> {
  if (process.platform !== "linux") {
    console.log(`[wg (mock)] Added peer ${publicKey} with IP ${assignedIp}/32 on interface ${WG_INTERFACE}`);
    return true;
  }

  try {
    const cmd = `sudo wg set ${WG_INTERFACE} peer "${publicKey}" allowed-ips "${assignedIp}/32"`;
    await execAsync(cmd);
    console.log(`[wg] Successfully registered peer ${publicKey} -> ${assignedIp}/32 on ${WG_INTERFACE}`);
    return true;
  } catch (err) {
    console.error(`[wg] Failed to add peer to WireGuard:`, err);
    return false;
  }
}

/**
 * Removes a peer from the live WireGuard interface immediately.
 */
export async function removePeerFromWireGuard(publicKey: string): Promise<boolean> {
  if (process.platform !== "linux") {
    console.log(`[wg (mock)] Removed peer ${publicKey} from interface ${WG_INTERFACE}`);
    return true;
  }

  try {
    const cmd = `sudo wg set ${WG_INTERFACE} peer "${publicKey}" remove`;
    await execAsync(cmd);
    console.log(`[wg] Successfully removed peer ${publicKey} from ${WG_INTERFACE}`);
    return true;
  } catch (err) {
    console.error(`[wg] Failed to remove peer from WireGuard:`, err);
    return false;
  }
}
