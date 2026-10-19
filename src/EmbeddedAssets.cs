using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Markup;

namespace LumaKeys {
    // Resource packaging is not a guarantee against reverse engineering.
    internal static class EmbeddedAssets {
        public static Window LoadInterface() {
            using(var encrypted=Assembly.GetExecutingAssembly().GetManifestResourceStream("LumaKeys.Interface")) {
                if(encrypted==null)throw new InvalidDataException("程序界面资源缺失，请重新下载完整客户端。");
                using(var aes=Aes.Create()) {
                    aes.Key=Convert.FromBase64String(EmbeddedResourceKey.Key);
                    aes.IV=Convert.FromBase64String(EmbeddedResourceKey.IV);
                    using(var decryptor=aes.CreateDecryptor())
                    using(var clear=new CryptoStream(encrypted,decryptor,CryptoStreamMode.Read))
                    using(var buffer=new MemoryStream()) {
                        clear.CopyTo(buffer);buffer.Position=0;return (Window)XamlReader.Load(buffer);
                    }
                }
            }
        }
    }
}
