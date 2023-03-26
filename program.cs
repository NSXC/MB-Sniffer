using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

class Program
{
    static string GetVendorName(byte[] vendorBytes)
    {
        string[] lines = File.ReadAllLines("oui.txt");//DIVICE DATABASE NOT FORMATED WELL ADDING SQLITE SOON
        foreach (string line in lines)
        {
            if (line.StartsWith(BitConverter.ToString(vendorBytes).Replace("-", ":")))
            {
                string[] parts = line.Split('\t');
                return parts[2];
            }
        }
        return "Unknown";
    }

    static string GetDeviceType(string vendor)
    {
        switch (vendor.ToUpper())
        {
            case "APPLE, INC.":
                return "iPhone";
            case "SAMSUNG ELECTRONICS CO.":
                return "Samsung phone";
            default:
                return "Unknown";
        }
    }

    static void Main(string[] args)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
        var localEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), 0);
        socket.Bind(localEndPoint);
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
        socket.IOControl(IOControlCode.ReceiveAll, BitConverter.GetBytes(1), null);
        while (true)
        {
            byte[] buffer = new byte[4096];
            int bytesRead = socket.Receive(buffer);
            byte[] sourceIPBytes = new byte[4];
            Buffer.BlockCopy(buffer, 12, sourceIPBytes, 0, 4);
            IPAddress sourceIPAddress = new IPAddress(sourceIPBytes);
            byte[] destIPBytes = new byte[4];
            Buffer.BlockCopy(buffer, 16, destIPBytes, 0, 4);
            IPAddress destIPAddress = new IPAddress(destIPBytes);
            Console.WriteLine($"Source IP: {sourceIPAddress}");
            if (IsWebsite(destIPAddress))
            {
                Console.WriteLine($"Destination URL: http://{destIPAddress}");
            }
            else
            {
                Console.WriteLine($"Destination IP: {destIPAddress}");
                GetDeviceInfo(destIPAddress);
            }
        }
    }

    static bool IsWebsite(IPAddress ipAddress)
    {
        if (IPAddress.IsLoopback(ipAddress))
        {
            return false;
        }
        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return false;
        }
        byte[] ipBytes = ipAddress.GetAddressBytes();
        if (ipBytes[0] == 10 ||
            (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) ||
            (ipBytes[0] == 192 && ipBytes[1] == 168))
        {
            return false;
        }
        return true;
    }
    static void GetDeviceInfo(IPAddress ipAddress)
    {
        byte[] macAddress = new byte[6];
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Raw);
        socket.Bind(new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
        byte[] buffer = new byte[28];
        buffer[0] = 0xFF;
        buffer[1] = 0xFF;
        buffer[2] = 0xFF;
        buffer[3] = 0xFF;
        buffer[4] = 0xFF;
        buffer[5] = 0xFF;
        Buffer.BlockCopy(ipAddress.GetAddressBytes(), 0, buffer, 6, 4);
        socket.SendTo(buffer, new IPEndPoint(ipAddress, 0));
        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
        buffer = new byte[4096];
        int bytesRead = socket.ReceiveFrom(buffer, ref endPoint);
        if (bytesRead >= 42 && buffer[12] == 0x08 && buffer[13] == 0x00 && buffer[23] == 0x06)
        {
            // Found a valid ARP packet
            Buffer.BlockCopy(buffer, 22, macAddress, 0, 6);
            Console.WriteLine($"  - MAC Address: {BitConverter.ToString(macAddress)}");
            byte[] vendorBytes = new byte[3];
            Buffer.BlockCopy(macAddress, 0, vendorBytes, 0, 3);
            string vendor = GetVendorName(vendorBytes);
            Console.WriteLine($"  - Vendor: {vendor}");
            Console.WriteLine($"  - Device Type: {GetDeviceType(vendor)}");
        }
    }
}
