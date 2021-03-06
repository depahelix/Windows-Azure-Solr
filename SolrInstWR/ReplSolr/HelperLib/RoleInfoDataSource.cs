﻿#region Copyright Notice
/*
Copyright © Microsoft Open Technologies, Inc.
All Rights Reserved
Apache 2.0 License

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.
*/
#endregion

namespace HelperLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.StorageClient;
    using Microsoft.WindowsAzure.ServiceRuntime;
    using System.Net;

    public class RoleInfoDataSource
    {
        private const string entitySetName = "RoleEndPoints";
        // Storage services account information.
        private CloudStorageAccount storageAccount;

        // Context that allows the use of Windows Azure Table Services.
        private RoleInfoDataContext context;

        public RoleInfoDataSource()
        {
            this.storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            //this.storageAccount = CloudStorageAccount.FromConfigurationSetting("DataConnectionString");
            //CloudTableClient.CreateTablesFromModel(typeof(RoleInfoDataContext), storageAccount.TableEndpoint.AbsoluteUri, storageAccount.Credentials);

            CloudTableClient client = new CloudTableClient(storageAccount.TableEndpoint.AbsoluteUri, storageAccount.Credentials);
            client.CreateTableIfNotExist(entitySetName);

            this.context = new RoleInfoDataContext(storageAccount.TableEndpoint.AbsoluteUri, storageAccount.Credentials);
            this.context.RetryPolicy = RetryPolicies.Retry(3, TimeSpan.FromSeconds(1));
        }

        public IEnumerable<RoleInfoEntity> Select()
        {
            var results = from r in this.context.RoleInfoEntity
                          select r;
            return results;
        }

        public void AddRoleInfoEntity(RoleInfoEntity newItem)
        {
            this.context.AddObject(entitySetName, newItem);
            this.context.SaveChanges();
        }

        public void UpdateRoleInfoEntity(string rowId, string roleId, string endpoint, bool isSolrMaster)
        {
            var results = from r in this.context.RoleInfoEntity
                                 where r.RowKey == rowId
                                 select r;

            var roleInfoEntity = results.FirstOrDefault<RoleInfoEntity>();

            roleInfoEntity.RoleId = roleId;
            roleInfoEntity.IPString = endpoint;
            roleInfoEntity.IsSolrMaster = isSolrMaster;

            this.context.UpdateObject(roleInfoEntity);
            this.context.SaveChanges();
        }

        public IPEndPoint GetMasterEndpoint()
        {
            var result = (from r in this.context.CreateQuery<RoleInfoEntity>(entitySetName)
                         where r.IsSolrMaster == true
                         select r).FirstOrDefault<RoleInfoEntity>();

            if (result != null)
            {
                IPEndPoint endpoint = IPEndpointFromParts(result.IPString, result.Port);
                return endpoint;
            }
            return null;
        }

        public IPEndPoint GetSlaveEndpoint(string roleId)
        {
            var result = (from r in this.context.CreateQuery<RoleInfoEntity>(entitySetName)
                          where r.RoleId == roleId && r.IsSolrMaster == false
                           select r).FirstOrDefault<RoleInfoEntity>();

            if (result != null)
            {
                IPEndPoint endpoint = IPEndpointFromParts(result.IPString, result.Port);
                return endpoint;
            }
            return null;
        }

        private static IPEndPoint IPEndpointFromParts(string ipString, int port)
        {
            IPAddress address = IPAddress.Parse(ipString);
            IPEndPoint endpoint = new IPEndPoint(address, port);
            return endpoint;
        }

    }
}
