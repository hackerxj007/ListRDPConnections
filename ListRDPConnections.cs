﻿using System;
using Microsoft.Win32;
using Microsoft.VisualBasic.Devices;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Xml;

class ListRDPConnections
{
    private static RegistryKey rk;
	private static string prefix = @"C:\Users\";

	static void ListRDPOutConnections()
	{
		Console.WriteLine("RDP外连:");

        List<string> sids = new List<string>(Registry.Users.GetSubKeyNames());
		
		// Load NTUSER.DAT
		foreach (string dic in Directory.GetDirectories(prefix))
		{
			try
			{
				string subkey = "S-123456789-" + dic.Replace(prefix, "");
				string sid = RegistryInterop.Load(subkey, $@"{dic}\NTUSER.DAT");
				sids.Add(sid);
			}
			catch
			{
				continue;
            }
        }

		// Dump RDP Connection History
		foreach (string sid in sids)
		{
			if (!sid.StartsWith("S-") || sid.EndsWith("Classes") || sid.Length < 10)
				continue;

            Dictionary<string, string> history = GetRegistryValues(sid);
            if (history.Count != 0)
			{
				Console.WriteLine($"{sid}:");
				foreach (var item in history)
				{
					Console.WriteLine($"{item.Key}	{item.Value}");
				}
				Console.WriteLine();
			}

			if (sid.StartsWith("S-123456789-"))
			{
				UnLoadHive(sid);
			}
		}
	}

	static void UnLoadHive(string sid)
	{
		if (sid.StartsWith("S-123456789-"))
		{
			RegistryInterop.UnLoad(sid);
		}
	}

	static string GetOSName()
	{
		return new ComputerInfo().OSFullName;
	}

	static Dictionary<string, string> GetRegistryValues(string sid)
	{
		Dictionary<string, string> values = new Dictionary<string, string>();
		string baseKey = $@"{sid}\Software\Microsoft\Terminal Server Client\";

		try
		{
			// Default
			rk = Registry.Users.OpenSubKey(baseKey + "Default");
			foreach (string mru in rk.GetValueNames())
			{
				values.Add(rk.GetValue(mru).ToString(), "");
			}
			rk.Close();

			// Servers
			rk = Registry.Users.OpenSubKey(baseKey + "Servers");
			string[] addresses = rk.GetSubKeyNames();
			rk.Close();
			foreach (string address in addresses)
			{
				rk = Registry.Users.OpenSubKey($@"{baseKey}Servers\{address}");
				string user = rk.GetValue("UsernameHint").ToString();
				if (values.ContainsKey(address))
				{
					values[address] = user;
				}
				rk.Close();
			}
		}
		catch
		{
		}

		return values;
	}

	static void ListRDPInConnections()
	{
		Console.WriteLine("RDP内连:");

		string logTypeSuccess = "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational";
		string logTypeAll = "Microsoft-Windows-TerminalServices-RemoteConnectionManager/Operational";
		string querySuccess = "*[System/EventID=21] or *[System/EventID=25]";
		string queryAll = "*[System/EventID=1149]";

		var historySuccess = ListEventvwrRecords(logTypeSuccess, querySuccess);
		var historyAll = ListEventvwrRecords(logTypeAll, queryAll, true);

		Console.WriteLine("Login Successful:");
		foreach (string history in historySuccess)
		{
			Console.WriteLine(history);
			int index = historyAll.IndexOf(history);
			if (index != -1)
			{
				historyAll.RemoveAt(index);
            }
        }

		Console.WriteLine("Login Failed:");
		foreach (string history in historyAll)
		{
			Console.WriteLine(history);
        }
	}

	static List<string> ListEventvwrRecords(string logType, string query, bool flag=false)
	{
		List<string> values = new List<string>();

		var elQuery = new EventLogQuery(logType, PathType.LogName, query);
		var elReader = new EventLogReader(elQuery);

		for (EventRecord eventInstance = elReader.ReadEvent(); eventInstance != null; eventInstance = elReader.ReadEvent())
		{
			XmlDocument doc = new XmlDocument();
			doc.LoadXml(eventInstance.ToXml());
			XmlNodeList userData = doc.FirstChild.LastChild.FirstChild.ChildNodes;
			string user = userData[0].InnerText;
			string address = userData[2].InnerText;

			if (flag == true)
			{
				string domain = userData[1].InnerText;
				user = domain + (domain != "" ? "\\" : "")  + user;
			}
			string value = $"{address}	{user}";

			if (address != "本地" && !values.Exists(t => t == value))
			{
				values.Add(value);
			}
		}

		return values;
	}

	static void Main(string[] args)
	{
		Console.WriteLine("Author: HeartSky");
		Console.WriteLine("");

		ListRDPOutConnections();
		ListRDPInConnections();
	}
}