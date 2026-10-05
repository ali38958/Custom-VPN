import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/prisma";
import { verifyPassword } from "@/lib/auth";
import { getClientIp } from "@/lib/client-ip";

export async function POST(req: NextRequest) {
  try {
    const { username, password, deviceId, deviceName, deviceInfo } = await req.json();

    if (!username || !password || !deviceId) {
      return NextResponse.json({ error: "Username, password, and deviceId are required" }, { status: 400 });
    }

    const user = await prisma.user.findUnique({
      where: { username },
      include: { session: true, network: true },
    });

    if (!user) return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
    if (user.disabled) return NextResponse.json({ error: "Account suspended" }, { status: 403 });

    const isValid = await verifyPassword(password, user.passwordHash);
    if (!isValid) return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });

    const clientIp = getClientIp(req);

    if (user.session) {
      if (user.session.deviceId !== deviceId) {
        return NextResponse.json({ error: "Device locked" }, { status: 409 });
      }
      await prisma.session.update({
        where: { userId: user.id },
        data: {
          deviceName: deviceName || user.session.deviceName,
          deviceInfo: deviceInfo || user.session.deviceInfo,
          clientIp, lastSeenAt: new Date(), isOnline: true,
        },
      });
    } else {
      await prisma.session.create({
        data: {
          userId: user.id, deviceId, deviceName, deviceInfo, clientIp, isOnline: true,
        },
      });
    }

    return NextResponse.json({
      success: true,
      user: { id: user.id, username: user.username, assignedIp: user.assignedIp },
      network: user.network,
      serverConfig: {
        endpoint: "144.24.25.135:8443",
        subnet: user.network?.subnet || "10.77.0.0/24",
      },
      openvpnConfigUrl: req.nextUrl.origin + "/api/client/config?username=" + username,
      openvpnConfigText: `client
proto tcp-client
remote 144.24.25.135 8443
dev tun
resolv-retry infinite
nobind
persist-key
persist-tun
remote-cert-tls server
verify-x509-name server_uJhEEvcC0Jf8gGoz name
auth SHA256
auth-nocache
cipher AES-128-GCM
ignore-unknown-option data-ciphers
data-ciphers AES-128-GCM
ncp-ciphers AES-128-GCM
tls-client
tls-version-min 1.2
tls-cipher TLS-ECDHE-ECDSA-WITH-AES-128-GCM-SHA256
tls-ciphersuites TLS_AES_256_GCM_SHA384:TLS_AES_128_GCM_SHA256:TLS_CHACHA20_POLY1305_SHA256
ignore-unknown-option block-outside-dns
setenv opt block-outside-dns # Prevent Windows 10 DNS leak
verb 3
<ca>
-----BEGIN CERTIFICATE-----
MIIB2jCCAYCgAwIBAgIUbDwi07/tWPjo4d8sq9HZJ+1yZfcwCgYIKoZIzj0EAwIw
HjEcMBoGA1UEAwwTY25fYm9Sc0RkaTBzZlFVeFBCbjAeFw0yNjEwMDUxMzQwMDJa
Fw0zNjEwMDIxMzQwMDJaMB4xHDAaBgNVBAMME2NuX2JvUnNEZGkwc2ZRVXhQQm4w
WTATBgcqhkjOPQIBBggqhkjOPQMBBwNCAAQxb2co9h9jYstfb/wHkjcHWgEO2i7C
rOsMKLRqp9pF1hpODQ78+lprV3E241SLZXhQN2TyCiE3NQMFbgd7rGE3o4GbMIGY
MA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFLwvmGiwf1WDrMFaNZ+wbDW+qIiz
MFkGA1UdIwRSMFCAFLwvmGiwf1WDrMFaNZ+wbDW+qIizoSKkIDAeMRwwGgYDVQQD
DBNjbl9ib1JzRGRpMHNmUVV4UEJughRsPCLTv+1Y+Ojh3yyr0dkn7XJl9zALBgNV
HQ8EBAMCAQYwCgYIKoZIzj0EAwIDSAAwRQIgUvzsQkLUtxV/MO9ym5+1WMN4GbSY
fsCnmu9Phg8tP08CIQDfJlEA3+JqLaQ+HYMh6rII/CuasOY3+D90ytis6F8eHQ==
-----END CERTIFICATE-----
</ca>
<cert>
-----BEGIN CERTIFICATE-----
MIIB2zCCAYCgAwIBAgIRANuV9qEHVoEKd0+HqN2qAA8wCgYIKoZIzj0EAwIwHjEc
MBoGA1UEAwwTY25fYm9Sc0RkaTBzZlFVeFBCbjAeFw0yNjEwMDUxMzQwMDRaFw0z
NjEwMDIxMzQwMDRaMBIxEDAOBgNVBAMMB3Zwbl8wMDMwWTATBgcqhkjOPQIBBggq
hkjOPQMBBwNCAAQYBbfRymvKIo5OYxpoeRlEDSx0h2f5DayDlJL2JEBjp82aE2HB
vlbOmYh2R0g9a6xydq25PZDZ7WXSK0vZKpLoo4GqMIGnMAkGA1UdEwQCMAAwHQYD
VR0OBBYEFLak4F/S0koRjsIqTtMe9tVjTXqOMFkGA1UdIwRSMFCAFLwvmGiwf1WD
rMFaNZ+wbDW+qIizoSKkIDAeMRwwGgYDVQQDDBNjbl9ib1JzRGRpMHNmUVV4UEJu
ghRsPCLTv+1Y+Ojh3yyr0dkn7XJl9zATBgNVHSUEDDAKBggrBgEFBQcDAjALBgNV
HQ8EBAMCB4AwCgYIKoZIzj0EAwIDSQAwRgIhAKFuJsTF47RisnVjpTWzhqYGvac7
4GTZ6y/jqGsZ5/s7AiEAnH8+jBmc2GqG3kAMCHU4xfRNhNlFkt1YsTlFhxux2yU=
-----END CERTIFICATE-----
</cert>
<key>
-----BEGIN PRIVATE KEY-----
MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgZ7H25DaV8dTnRZ2N
fu9DaaJiRsse2PDlc/2HHoSiSXGhRANCAAQYBbfRymvKIo5OYxpoeRlEDSx0h2f5
DayDlJL2JEBjp82aE2HBvlbOmYh2R0g9a6xydq25PZDZ7WXSK0vZKpLo
-----END PRIVATE KEY-----
</key>
<tls-crypt-v2>
-----BEGIN OpenVPN tls-crypt-v2 client key-----
q2yt6h7JGjTrt0DNW6kVsi2sRJLiB6F0Yy89f16mfHAtyjgr9fAPKUMP8D/CCBlN
PM4whqSumDiE75g9IxiCW1eXgdKVM1C81qqtiOpS8Ax3ygeRkLZuklGsrP8wSJLf
XfiEYDRtBBWYMSKNqemYWVcp3jvjM81PtYR0hK1EeDc4C5rX0S0g/jbcGH04foTG
eBEh6iTZXkoIJtxk+D7gb16o1Ycw24C2/oeiBG3epyLuwrF+Gjs2mgz0ipigtEtE
jMrBeQutbMkumHJNCmzS3YVrNP5j3vv4YJjjETguUeknq9bSQwpO5nfksAZK028F
890CLx7QSFL+E03ccpI7NVNxWfjWUnRKpotR2zCPKviNjcCZfBnyApW7EkoJ6jMH
NgIo4K9CKIRC4CBeNNhZLnRn0lK+oqQ439PdRyUTb0JLfO/1ZmFwEi/wxT17m9go
W06CAE5KQyF3rZRmAJtJKsCSMCbKMHdX6tOM0EQNvDlgin30wel/+fAkAAY/G4KK
3YmPFF9B0uT9Hc9pJ5gsnMLMuaZDcKPXFUIb5wAROhUkujtAMNFXpuTb2RCmGyEb
z50Eeqc1SHTEtzk8vz7oxe33KpAWLxXeOYcJjXw6QHCd++bKIYXjwlOg9WovzMBs
gOOe3h+RCWElQ990alKYYDeQu6l9VEQmdEBTTqNt9HOFNam/rvlvfy3TH5bCJjLK
idW2xsW9AzYgAuOM0QRtvItfbDV1bywVKQEr
-----END OpenVPN tls-crypt-v2 client key-----
</tls-crypt-v2>`
    });
  } catch (error) {
    console.error("Client login error:", error);
    return NextResponse.json({ error: "Authentication failed" }, { status: 500 });
  }
}
