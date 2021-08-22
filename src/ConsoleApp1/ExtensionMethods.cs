using Microsoft.Build.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal static class ExtensionMethods
    {
        public static void SetCurrentHost(this BuildManager buildManager, string hostExePath)
        {
            if (!File.Exists(hostExePath))
            {
                return;
            }

            Type buildManagerType = buildManager.GetType();

            FieldInfo nodeManagerFieldInfo = buildManagerType.GetField("_nodeManager", BindingFlags.Instance | BindingFlags.NonPublic);

            if (nodeManagerFieldInfo == null)
            {
                return;
            }

            object nodeManager = nodeManagerFieldInfo.GetValue(buildManager);

            if (nodeManager == null)
            {
                return;
            }

            Type nodeManagerType = nodeManager.GetType();

            FieldInfo outOfProcNodeProviderFieldInfo = nodeManagerType.GetField("_outOfProcNodeProvider", BindingFlags.Instance | BindingFlags.NonPublic);

            if (outOfProcNodeProviderFieldInfo == null)
            {
                return;
            }

            object outOfProcNodeProvider = outOfProcNodeProviderFieldInfo.GetValue(nodeManager);

            if (outOfProcNodeProvider == null)
            {
                return;
            }

            Type nodeProviderOutOfProcBaseType = outOfProcNodeProvider.GetType().BaseType;

            if (nodeProviderOutOfProcBaseType == null)
            {
                return;
            }

            FieldInfo currentHostFieldInfo = nodeProviderOutOfProcBaseType.GetField("CurrentHost", BindingFlags.Static | BindingFlags.NonPublic);

            if (currentHostFieldInfo == null)
            {
                return;
            }

            currentHostFieldInfo.SetValue(outOfProcNodeProvider, hostExePath);
        }
    }
}
