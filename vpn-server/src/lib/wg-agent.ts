import net from "net";

const WG_AGENT_SOCKET = process.env.WG_AGENT_SOCKET || "/run/wg-agent.sock";

/**
 * Sends a command to the wg-agent daemon listening on the local unix socket.
 * On Windows/development environments where the socket does not exist,
 * it safely logs and resolves without crashing.
 */
export async function sendWgAgentCommand(command: "sync" | "status"): Promise<string> {
  // If on non-linux or socket path does not start with '/', simulate success in development
  if (process.platform === "win32" || !WG_AGENT_SOCKET.startsWith("/")) {
    console.log(`[wg-agent (mock)] Command "${command}" simulated on ${process.platform}`);
    return JSON.stringify({ status: "ok", simulated: true });
  }

  return new Promise((resolve) => {
    const client = net.createConnection({ path: WG_AGENT_SOCKET }, () => {
      client.write(`${command}\n`);
    });

    let data = "";
    client.on("data", (chunk) => {
      data += chunk.toString();
    });

    client.on("end", () => {
      resolve(data.trim());
    });

    client.on("error", (err) => {
      console.warn(`[wg-agent] Could not connect to socket ${WG_AGENT_SOCKET}:`, err.message);
      // Resolve with fallback so requests don't fail completely if agent is starting up
      resolve(JSON.stringify({ status: "error", error: err.message }));
    });

    // 3 second timeout
    client.setTimeout(3000, () => {
      client.destroy();
      resolve(JSON.stringify({ status: "timeout" }));
    });
  });
}

export async function triggerWgSync() {
  return sendWgAgentCommand("sync");
}
