# AutoDNS

This project allows switching DNS settings for selected network interfaces.  
It now supports automatic DNS switching based on running programs.

## Program-specific DNS

Edit the `programs.json` file to map program executables to DNS profiles:

```
[
  {
    "ProgramPath": "C:\\Path\\To\\app.exe",
    "Profile": "Cloudflare"
  }
]
```

When a listed program is detected running, AutoDNS applies the specified DNS.  
Once all monitored programs exit, it automatically switches back to AdGuard DNS.