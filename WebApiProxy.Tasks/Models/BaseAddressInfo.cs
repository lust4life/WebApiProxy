﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using WebApiProxy.Tasks.Infrastructure;

namespace WebApiProxy.Tasks.Models
{
    public class BaseAddressInfo
    {
        public static string ConsulUri { get; private set; }

        public static string TraefikAddress;
        protected static ConcurrentDictionary<string, string> AddressInfos = new ConcurrentDictionary<string, string>();
        protected static Dictionary<string, bool> ServiceDic = new Dictionary<string, bool>();

        protected BaseAddressInfo(string consulUri)
        {
            ConsulUri = consulUri;
            ServiceDic["User"] = true;
            ServiceDic["UserName"] = true;
        }

        public static string GetAddress(string serviceName)
        {
            bool serviceUseTraefik;
            ServiceDic.TryGetValue(serviceName, out serviceUseTraefik);

            // 如果使用 traefik， 则默认提供降级服务。
            if (serviceUseTraefik)
            {
                if (!string.IsNullOrEmpty(TraefikAddress))
                {
                    return string.Format("{0}/go-{1}/", TraefikAddress, serviceName);
                }

                var traefikInfo = ConsulHelper.DiscoveryService(ConsulUri, ConsulHelper.TraefikServiceName);
                if (traefikInfo != null)
                {
                    TraefikAddress = String.Format("http://{0}:{1}/",
                                                   traefikInfo.Address,
                                                   traefikInfo.Port);
                    return string.Format("{0}/go-{1}/", TraefikAddress, serviceName);
                }

                // 说明 traefik 不可用，则获取原始信息，提供降级服务
                var serviceInfo = ConsulHelper.DiscoveryService(ConsulUri, serviceName);
                if (serviceInfo != null)
                {
                    return String.Format("http://{0}:{1}/",
                                         serviceInfo.Address,
                                         serviceInfo.Port);
                }

                return null;
            }
            else
            {
                string address;
                AddressInfos.TryGetValue(serviceName, out address);
                return address;
            }
        }

        public virtual void LoadAddressInfo()
        {
            if (string.IsNullOrEmpty(TraefikAddress))
            {
                var traefikInfo = ConsulHelper.DiscoveryService(ConsulUri, ConsulHelper.TraefikServiceName);
                if (traefikInfo != null)
                {
                    TraefikAddress = String.Format("http://{0}:{1}/",
                                                   traefikInfo.Address,
                                                   traefikInfo.Port);
                }
            }

            foreach (var serviceUseTraefik in ServiceDic)
            {
                var serviceName = serviceUseTraefik.Key;
                var isServiceUseTraefik = serviceUseTraefik.Value;

                if (!isServiceUseTraefik)
                {
                    var serviceInfo = ConsulHelper.DiscoveryService(ConsulUri, serviceName);
                    if (serviceInfo == null)
                    {
                        throw new Exception(String.Format("consul 中没有 {0} 可用信息", serviceName));
                    }

                    AddressInfos[serviceName] = String.Format("http://{0}:{1}/",
                                                              serviceInfo.Address,
                                                              serviceInfo.Port);
                }
            }
        }

        public static void TryReportTraefikError(string serviceName)
        {
            bool serviceUseTraefik;
            ServiceDic.TryGetValue(serviceName, out serviceUseTraefik);
            if (serviceUseTraefik)
            {
                TraefikAddress = null;
            }
        }
    }
}