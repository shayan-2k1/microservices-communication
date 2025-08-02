using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using GrpcService.Protos;
using Shared.Models;

namespace Shared.Mapper
{
    public class MapperProfile:Profile
    {
        public MapperProfile() 
        {
            CreateMap<ProductResponse, ProductDTO>().ReverseMap();
        }
    }
}
