# Enumerate UPNP devices
import argparse
import socket

parser = argparse.ArgumentParser()
parser.add_argument('--interface', help='Send out M-SEARCH on the interface with the given address.')
args = parser.parse_args()

# M-Search message body
MS = \
    'M-SEARCH * HTTP/1.1\r\n' \
    'HOST:239.255.255.250:1900\r\n' \
    'ST:ssdp:all\r\n' \
    'MX:2\r\n' \
    'MAN:"ssdp:discover"\r\n' \
    '\r\n'

# Set up a UDP socket for multicast
sct = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
sct.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_TTL, 2)
if args.interface:
    try:
        sct.setsockopt(socket.IPPROTO_IP, socket.IP_MULTICAST_IF, socket.inet_aton(args.interface))
    except:
        print("Interface address is not valid in its context!")
sct.settimeout(2)

# Send M-Search message to multicast address for UPNP
sct.sendto(MS.encode('utf-8'), ('239.255.255.250', 1900))

# Listen and capture returned responses
try:
    while True:
        data, addr = sct.recvfrom(8192)  # buffer size
        print(f'{addr}:')
        print(data.decode('utf-8')
              .replace('\\r\\n', '\r\n').replace('\\n', '\n').replace('\\r', '\r'))
except socket.timeout:
    pass
