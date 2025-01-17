﻿using AutoMapper;
using AzureDesignStudio.Core.DTO;
using AzureDesignStudio.Core.Models;
using Blazor.Diagrams.Core.Models;

namespace AzureDesignStudio.Core.VirtualNetwork
{
    public class AddressSpace
    {
        // For AntList binding in UI.
        public string AddressPrefix { get; set; } = string.Empty;
    }
    public class VirtualNetworkModel : AzureGroupBase
    {
        public VirtualNetworkModel() : base()
        {
            AddPort(new VirtualNetworkPort(this, PortAlignment.Left));
            AddPort(new VirtualNetworkPort(this, PortAlignment.Top));
            AddPort(new VirtualNetworkPort(this, PortAlignment.Right));
            AddPort(new VirtualNetworkPort(this, PortAlignment.Bottom));
        }
        public override string ServiceName => "Virtual Network";
        public override Type? DataFormType => typeof(VirtualNetworkForm);
        public override string ApiVersion => "2021-03-01";
        public override string ResourceType => "Microsoft.Network/virtualNetworks";
        public List<AddressSpace> IpSpace { get; set; } = new List<AddressSpace>()
        {
            new AddressSpace()
            {
                AddressPrefix = "10.0.0.0/16"
            }
        };
        public List<SubnetModel> Subnets
        {
            get
            {
                if (Children?.Count > 0)
                    return Children.Where(c => c is SubnetModel).Select(c => (SubnetModel)c).ToList();

                return new List<SubnetModel>();
            }
        }
        public SubnetModel AddSubnet(SubnetModel subnet)
        {
            var count = Subnets.Count;
            subnet.SetPosition(Position.X + Padding + count * 5, Position.Y + Padding + count * 5);

            AddChild(subnet);
            return subnet;
        }

        public override IList<IDictionary<string, dynamic>> GetArmResources()
        {
            var result = new List<IDictionary<string, dynamic>>();

            Properties.Clear();

            Properties.Add("addressSpace", new Dictionary<string, dynamic>()
            {
                {"addressPrefixes", IpSpace.Select(ip => ip.AddressPrefix).ToList()},
            });

            result.Add(new Dictionary<string, dynamic>()
            {
                {"type", ResourceType },
                {"apiVersion", ApiVersion },
                {"name", Name},
                {"location", Location},
                {"properties", Properties },
            });

            foreach(var subnet in Subnets)
            {
                // Deploy subnets as separate resources.
                // It is necessary when there are other resources which depend on subnets.
                result.AddRange(subnet.GetArmResources());
            }

            // Vnet peering needs to be placed out of Vnet as it requires `dependsOn` to work. 
            // Child resource naming: https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/child-resource-name-type
            var ports = Ports.Where(p => p.Links.Count > 0).ToList();
            foreach (var port in ports)
            {
                foreach (var link in port.Links)
                {
                    var node = link.TargetPort?.Parent != this ? link.TargetPort?.Parent : link.SourcePort?.Parent;
                    if (node is VirtualNetworkModel n)
                    {
                        var peering = new VirtualNetworkPeeringModel()
                        {
                            Name = this.Name + "/" + n.Name + "-peering",
                            RemoteVirtualNetworkId = n.ResourceId,
                        };
                        peering.DependsOn.Add(ResourceId);
                        peering.DependsOn.Add(n.ResourceId);
                        result.Add(peering.GetArmResource());
                    }
                }
            }

            return result;
        }

        public override AzureNodeDto GetNodeDto(IMapper mapper)
        {
            var vnetDto = mapper.Map<VirtualNetworkDto>(this);

            return vnetDto;
        }
    }
}
