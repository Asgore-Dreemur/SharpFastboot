using LibSparseSharp;
using SharpFastboot.DataModel;
using SharpFastboot.Usb;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpFastboot
{
    public class FastbootUtil
    {
        public UsbDevice UsbDevice { get; private set; }

        public FastbootUtil(UsbDevice usb) => UsbDevice = usb;
        public static int ReadTimeoutSeconds = 30;
        public static int OnceSendDataSize = 1024 * 1024;
        public static int SparseMaxDownloadSize = 256 * 1024 * 1024;

        public event EventHandler<FastbootReceivedFromDeviceEventArgs>? ReceivedFromDevice;
        public event EventHandler<(long, long)>? DataTransferProgressChanged;
        public event EventHandler<string>? CurrentStepChanged;

        /// <summary>
        /// 处理请求
        /// </summary>
        public FastbootResponse HandleResponse()
        {
            FastbootResponse response = new FastbootResponse();
            DateTime start = DateTime.Now;
            while ((DateTime.Now - start) < TimeSpan.FromSeconds(ReadTimeoutSeconds))
            {
                byte[] data;
                try
                {
                    data = UsbDevice.Read(256);
                }
                catch (Exception e)
                {
                    response.Result = FastbootState.Fail;
                    response.Response = "status read failed: " + e.Message;
                    return response;
                }

                if (data.Length == 0) continue;

                string devStatus = Encoding.UTF8.GetString(data);
                if (devStatus.Length < 4)
                {
                    response.Result = FastbootState.Fail;
                    response.Response = "status malformed";
                    return response;
                }

                string prefix = devStatus.Substring(0, 4);
                string content = devStatus.Substring(4);

                if (prefix == "OKAY" || prefix == "FAIL")
                {
                    response.Result = prefix == "OKAY" ? FastbootState.Success : FastbootState.Fail;
                    response.Response = content;
                    return response;
                }
                else if (prefix == "INFO")
                {
                    response.Info.Add(content);
                    ReceivedFromDevice?.Invoke(this, new FastbootReceivedFromDeviceEventArgs(FastbootState.Info, content));
                    start = DateTime.Now;
                }
                else if (prefix == "TEXT")
                {
                    response.Text += content;
                    ReceivedFromDevice?.Invoke(this, new FastbootReceivedFromDeviceEventArgs(FastbootState.Text, null, content));
                    start = DateTime.Now;
                }
                else if (prefix == "DATA")
                {
                    response.Result = FastbootState.Data;
                    response.DataSize = int.Parse(content, System.Globalization.NumberStyles.HexNumber);
                    return response;
                }
                else
                {
                    response.Result = FastbootState.Unknown;
                    response.Response = devStatus;
                    return response;
                }
            }
            response.Result = FastbootState.Timeout;
            return response;
        }

        /// <summary>
        /// 发送命令
        /// </summary>
        public FastbootResponse RawCommand(string command)
        {
            byte[] cmdBytes = Encoding.UTF8.GetBytes(command);
            try
            {
                if (UsbDevice.Write(cmdBytes, cmdBytes.Length) != cmdBytes.Length)
                    return new FastbootResponse { Result = FastbootState.Fail, Response = "command write failed (short transfer)" };
            }
            catch (Exception e)
            {
                return new FastbootResponse { Result = FastbootState.Fail, Response = "command write failed: " + e.Message };
            }
            return HandleResponse();
        }

        public FastbootResponse Reboot(string target = "system")
        {
            if (target == "recovery") return RawCommand("reboot-recovery");
            if (target == "bootloader") return RawCommand("reboot-bootloader");
            if (target == "fastboot") return RawCommand("reboot-fastboot");
            return RawCommand("reboot-" + target);
        }

        /// <summary>
        /// 获取所有属性
        /// </summary>
        public Dictionary<string, string> GetVarAll()
        {
            return RawCommand("getvar:all")
                .ThrowIfError()
                .Info.ToDictionary(str => str.Substring(0, str.LastIndexOf(":")),
                                    str => str.Substring(str.LastIndexOf(":") + 2).TrimStart());
        }

        /// <summary>
        /// 获取单个属性
        /// </summary>
        public string GetVar(string key) => RawCommand("getvar:" + key).ThrowIfError().Response;

        /// <summary>
        /// 获取插槽个数
        /// </summary>
        public int GetSlotCount()
        {
            int slot_count = 1;
            string count = GetVar("slot-count");
            int.TryParse(count, out slot_count);
            return slot_count;
        }

        /// <summary>
        /// 下载数据
        /// </summary>
        public FastbootResponse DownloadData(byte[] data)
        {
            FastbootResponse response = RawCommand("download:" + data.Length.ToString("x8"));
            if (response.Result == FastbootState.Fail)
                return response;
            UsbDevice.Write(data, data.Length);
            return HandleResponse();
        }

        /// <summary>
        /// 下载数据
        /// </summary>
        public FastbootResponse DownloadData(Stream stream, long length, bool onEvent = true)
        {
            FastbootResponse response = RawCommand("download:" + length.ToString("x8"));
            if (response.Result == FastbootState.Fail)
                return response;
            byte[] buffer = new byte[OnceSendDataSize];
            long bytesRead = 0;
            while (true)
            {
                int readSize = stream.Read(buffer, 0, buffer.Length);
                if (readSize <= 0) break;
                UsbDevice.Write(buffer, readSize);
                bytesRead += readSize;
                if (onEvent)
                    DataTransferProgressChanged?.Invoke(this, (bytesRead, length));
            }
            return HandleResponse();
        }

        /// <summary>
        /// 从设备上传数据 (对应协议中的上传)
        /// </summary>
        public FastbootResponse UploadData(string command, Stream output)
        {
            FastbootResponse response = RawCommand(command);
            if (response.Result != FastbootState.Data)
                throw new Exception("Unexpected response for upload: " + response.Result);

            int size = response.DataSize;
            int bytesDownloaded = 0;
            while (bytesDownloaded < size)
            {
                int toRead = Math.Min(OnceSendDataSize, size - bytesDownloaded);
                byte[] data = UsbDevice.Read(toRead);
                if (data == null || data.Length == 0) throw new Exception("Unexpected EOF from USB.");
                output.Write(data, 0, data.Length);
                bytesDownloaded += data.Length;
                DataTransferProgressChanged?.Invoke(this, (bytesDownloaded, size));
            }

            return HandleResponse(); // 最后应该收到一个 OKAY
        }

        /// <summary>
        /// 快照更新操作 (Virtual A/B)
        /// </summary>
        public FastbootResponse SnapshotUpdate(string action = "cancel")
        {
            if (action != "cancel" && action != "merge")
                throw new ArgumentException("SnapshotUpdate action must be 'cancel' or 'merge'");
            return RawCommand("snapshot-update:" + action);
        }

        /// <summary>
        /// 从分区回读并抓取数据 (fetch)
        /// </summary>
        public FastbootResponse Fetch(string partition, string outputPath, long offset = 0, long size = -1)
        {
            string cmd = "fetch:" + partition;
            if (offset > 0 || size > 0)
            {
                cmd += ":" + offset.ToString("x8");
                if (size > 0)
                {
                    cmd += ":" + size.ToString("x8");
                }
            }

            using var fs = File.Create(outputPath);
            return UploadData(cmd, fs);
        }

        /// <summary>
        /// 下载并抓取已分阶段的数据 (staged data)
        /// </summary>
        public void GetStaged(string outputPath)
        {
            using var fs = File.Create(outputPath);
            UploadData("get_staged", fs);
        }

        /// <summary>
        /// 刷入非稀疏镜像(Already Error check)
        /// </summary>
        public FastbootResponse FlashUnsparseImage(string partition, Stream stream, long length)
        {
            CurrentStepChanged?.Invoke(this, $"Sending {partition}");
            DownloadData(stream, length).ThrowIfError();
            CurrentStepChanged?.Invoke(this, $"Flashing {partition}");
            return RawCommand("flash:" + partition).ThrowIfError();
        }

        /// <summary>
        /// 刷入稀疏镜像(Already Error check)
        /// </summary>
        public FastbootResponse FlashSparseImage(string partition, string filePath)
        {
            int count = 1;
            FastbootResponse response = new FastbootResponse();
            int max_download_size = SparseMaxDownloadSize;
            int.TryParse(GetVar("max-download-size").TrimStart("0x"),
                System.Globalization.NumberStyles.HexNumber, null, out max_download_size);
            SparseFile sfile = SparseFile.FromImageFile(filePath);
            var parts = sfile.Resparse(max_download_size);
            foreach (var item in parts)
            {
                Stream stream = item.GetExportStream(0, item.Header.TotalBlocks);
                CurrentStepChanged?.Invoke(this, $"Sending {partition}({count} / {parts.Count})");
                DownloadData(stream, stream.Length).ThrowIfError();
                CurrentStepChanged?.Invoke(this, $"Flashing {partition}({count} / {parts.Count})");
                response = RawCommand("flash:" + partition);
                response.ThrowIfError();
                count++;
            }
            return response;
        }

        public string GetCurrentSlot() => GetVar("current-slot");

        /// <summary>
        /// 判断分区是否支持插槽
        /// </summary>
        public bool HasSlot(string partition)
        {
            try
            {
                string res = GetVar("has-slot:" + partition);
                return res == "yes";
            }
            catch
            {
                return false;
            }
        }

        public FastbootResponse SetActiveSlot(string slot) => RawCommand("set_active:" + slot);
        public FastbootResponse ErasePartition(string partition)
        {
            if (HasSlot(partition))
            {
                partition += "_" + GetCurrentSlot();
            }
            return RawCommand("erase:" + partition);
        }

        /// <summary>
        /// 智能刷入镜像 (根据魔数自动判断是否为稀疏镜像，并自动处理 A/B 插槽)
        /// </summary>
        public void FlashImage(string partition, string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);

            string targetPartition = partition;
            if (HasSlot(partition))
            {
                targetPartition = partition + "_" + GetCurrentSlot();
            }

            try
            {
                var header = SparseFile.PeekHeader(filePath);
                if (header.Magic == SparseFormat.SparseHeaderMagic)
                {
                    FlashSparseImage(targetPartition, filePath);
                }
                else
                {
                    using var fs = File.OpenRead(filePath);
                    FlashUnsparseImage(targetPartition, fs, fs.Length);
                }
            }
            catch
            {
                // 如果 PeekHeader 失败，退回到非稀疏
                using var fs = File.OpenRead(filePath);
                FlashUnsparseImage(targetPartition, fs, fs.Length);
            }
        }

        /// <summary>
        /// 执行 OEM 命令
        /// </summary>
        public FastbootResponse OemCommand(string oemCmd) => RawCommand("oem " + oemCmd);

        /// <summary>
        /// 执行 Flashing 子命令 (现代解锁命令)
        /// </summary>
        public FastbootResponse FlashingCommand(string subCmd) => RawCommand("flashing " + subCmd);

        public FastbootResponse FlashingUnlock() => FlashingCommand("unlock");
        public FastbootResponse FlashingLock() => FlashingCommand("lock");
        public FastbootResponse FlashingUnlockCritical() => FlashingCommand("unlock_critical");
        public FastbootResponse FlashingLockCritical() => FlashingCommand("lock_critical");
        public bool FlashingGetUnlockAbility()
        {
            var res = FlashingCommand("get_unlock_ability");
            return res.Response.Trim() == "1";
        }

        /// <summary>
        /// 继续启动过程
        /// </summary>
        public FastbootResponse Continue() => RawCommand("continue");

        /// <summary>
        /// 格式化分区
        /// </summary>
        public FastbootResponse FormatPartition(string partition) => RawCommand("format:" + partition);

        /// <summary>
        /// 创建逻辑分区
        /// </summary>
        public FastbootResponse CreateLogicalPartition(string partition, long size)
            => RawCommand($"create-logical-partition:{partition}:{size}");

        /// <summary>
        /// 删除逻辑分区
        /// </summary>
        public FastbootResponse DeleteLogicalPartition(string partition)
            => RawCommand($"delete-logical-partition:{partition}");

        /// <summary>
        /// 调整逻辑分区大小
        /// </summary>
        public FastbootResponse ResizeLogicalPartition(string partition, long size)
            => RawCommand($"resize-logical-partition:{partition}:{size}");

        /// <summary>
        /// 发送并引导内核 (不写入 Flash)
        /// </summary>
        public FastbootResponse Boot(byte[] data)
        {
            DownloadData(data).ThrowIfError();
            return RawCommand("boot");
        }

        /// <summary>
        /// 刷入由 kernel 和 ramdisk 混合生成的原始镜像
        /// </summary>
        public FastbootResponse FlashRaw(string partition, byte[] kernel, byte[]? ramdisk = null, byte[]? second = null, string? cmdline = null, string? name = null, uint base_addr = 0x10000000, uint page_size = 2048)
        {
            byte[] bootImg = CreateBootImage(kernel, ramdisk, second, cmdline, name, base_addr, page_size);
            DownloadData(bootImg).ThrowIfError();
            return RawCommand("flash:" + partition);
        }

        /// <summary>
        /// 生成 BootImage 数据
        /// </summary>
        public byte[] CreateBootImage(byte[] kernel, byte[]? ramdisk, byte[]? second, string? cmdline, string? name, uint base_addr, uint page_size)
        {
            BootImageHeader header = BootImageHeader.Create();
            header.KernelSize = (uint)kernel.Length;
            header.KernelAddr = base_addr + 0x00008000;
            header.RamdiskSize = (uint)(ramdisk?.Length ?? 0);
            header.RamdiskAddr = base_addr + 0x01000000;
            header.SecondSize = (uint)(second?.Length ?? 0);
            header.SecondAddr = base_addr + 0x00F00000;
            header.TagsAddr = base_addr + 0x00000100;
            header.PageSize = page_size;

            if (!string.IsNullOrEmpty(cmdline))
            {
                byte[] cmdBytes = Encoding.ASCII.GetBytes(cmdline);
                Array.Copy(cmdBytes, header.Cmdline, Math.Min(cmdBytes.Length, 512));
            }

            if (!string.IsNullOrEmpty(name))
            {
                byte[] nameBytes = Encoding.ASCII.GetBytes(name);
                Array.Copy(nameBytes, header.Name, Math.Min(nameBytes.Length, 16));
            }

            int headerSize = Marshal.SizeOf<BootImageHeader>();
            int headerPages = (headerSize + (int)page_size - 1) / (int)page_size;
            int kernelPages = (kernel.Length + (int)page_size - 1) / (int)page_size;
            int ramdiskPages = ((ramdisk?.Length ?? 0) + (int)page_size - 1) / (int)page_size;
            int secondPages = ((second?.Length ?? 0) + (int)page_size - 1) / (int)page_size;

            int totalSize = (headerPages + kernelPages + ramdiskPages + secondPages) * (int)page_size;
            byte[] buffer = new byte[totalSize];

            byte[] headerBytes = DataHelper.Struct2Bytes(header);
            Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length);
            Array.Copy(kernel, 0, buffer, headerPages * page_size, kernel.Length);
            if (ramdisk != null)
            {
                Array.Copy(ramdisk, 0, buffer, (headerPages + kernelPages) * page_size, ramdisk.Length);
            }
            if (second != null)
            {
                Array.Copy(second, 0, buffer, (headerPages + kernelPages + ramdiskPages) * page_size, second.Length);
            }

            return buffer;
        }

        /// <summary>
        /// 发送签名文件
        /// </summary>
        public FastbootResponse Signature(byte[] sigData)
        {
            DownloadData(sigData).ThrowIfError();
            return RawCommand("signature");
        }

        /// <summary>
        /// 校验 android-info.txt 中的需求
        /// </summary>
        public bool VerifyRequirements(string infoText)
        {
            var lines = infoText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("require ") || line.StartsWith("require-for-product:"))
                {
                    string content = line;
                    if (line.StartsWith("require-for-product:"))
                    {
                        var parts = line.Split(' ', 2);
                        string prod = parts[0].Substring(20);
                        string currentProd = GetVar("product");
                        if (currentProd != prod) continue; // 不适用于当前产品，跳过
                        content = parts.Length > 1 ? parts[1] : "";
                    }
                    else
                    {
                        content = line.Substring(8);
                    }

                    if (string.IsNullOrEmpty(content)) continue;

                    var requirements = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var req in requirements)
                    {
                        var kv = req.Split('=', 2);
                        if (kv.Length != 2) continue;

                        string key = kv[0];
                        string val = kv[1];

                        // 特殊映射
                        if (key == "board") key = "product";

                        string deviceVal = GetVar(key);
                        var allowedValues = val.Split('|');

                        if (!allowedValues.Contains(deviceVal))
                        {
                            throw new Exception($"Requirement check failed for {key}: expected {val}, but device has {deviceVal}");
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 执行 FlashAll (在指定目录下寻找并刷入基础分区)
        /// </summary>
        public void FlashAll(string productOutDir, bool wipe = false)
        {
            // 1. 校验 android-info.txt
            string infoPath = Path.Combine(productOutDir, "android-info.txt");
            if (File.Exists(infoPath))
            {
                VerifyRequirements(File.ReadAllText(infoPath));
            }

            // 2. 刷入各个分区
            // 标准动态分区和常用 A/B 分区列表
            string[] flashable = {
                "boot", "init_boot", "vendor_boot",
                "dtbo",
                "vbmeta", "vbmeta_system", "vbmeta_vendor",
                "recovery",
                "system", "vendor", "product", "system_ext", "odm",
                "vendor_dlkm", "odm_dlkm", "system_dlkm",
                "super" // 如果是 super 镜像（通常在 super.img 中，包含系统分区逻辑组合）
            };

            foreach (var part in flashable)
            {
                string filePath = Path.Combine(productOutDir, part + ".img");
                if (File.Exists(filePath))
                {
                    FlashImage(part, filePath);
                    // 处理签名文件
                    string sigPath = Path.Combine(productOutDir, part + ".sig");
                    if (File.Exists(sigPath))
                    {
                        Signature(File.ReadAllBytes(sigPath));
                    }
                }
            }

            if (wipe)
            {
                WipeUserData();
            }
        }

        /// <summary>
        /// 清除用户数据和缓存
        /// </summary>
        public void WipeUserData()
        {
            try { FormatPartition("userdata"); } catch { }
            try { FormatPartition("cache"); } catch { }
        }
    }
}
