﻿using AdvancedSharpAdbClient;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace APKInstaller.Helpers
{
    public static class AddressHelper
    {
        public static async Task<List<string>> GetAddressID(string mac)
        {
            List<string> addresses = new();
            Regex Regex = new($@"\s*(\d+.\d+.\d+.\d+)\s*{mac}\S*\s*\w+");
            List<string> lines = await CommandHelper.ExecuteShellCommand($"arp -a|findstr {mac}");
            foreach (string line in lines)
            {
                if (Regex.IsMatch(line))
                {
                    addresses.Add(Regex.Match(line).Groups[1].Value);
                }
            }
            return addresses;
        }

        public static async Task ConnectHyperV()
        {
            AdvancedAdbClient AdbClient = new();
            List<string> addresses = await GetAddressID("00-15-5d");
            foreach (string address in addresses)
            {
                AdbClient.Connect(address);
            }
        }
    }
}
