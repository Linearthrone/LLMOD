"""
House Victoria Bridge - TCP test client.
Sends JSON-line commands to the Unreal Editor bridge on port 17711.
"""
import socket
import json
import sys

HOST = "127.0.0.1"
PORT = 17711


def send_command(name: str, args: dict):
    payload = {
        "type": "command",
        "payload": {
            "name": name,
            "args": args
        }
    }
    line = json.dumps(payload, separators=(",", ":")) + "\n"
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect((HOST, PORT))
        s.sendall(line.encode("utf-8"))
        print(f"Sent: {line.strip()}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python victoria_bridge_client.py <command> [args...]")
        print("Examples:")
        print("  python victoria_bridge_client.py move_avatar x=100 y=0 z=0")
        print("  python victoria_bridge_client.py rotate_avatar yaw=90")
        print("  python victoria_bridge_client.py status")
        sys.exit(1)

    cmd = sys.argv[1]
    args = {}
    for token in sys.argv[2:]:
        if "=" in token:
            k, v = token.split("=", 1)
            try:
                v = float(v)
            except ValueError:
                pass
            args[k] = v

    send_command(cmd, args)
