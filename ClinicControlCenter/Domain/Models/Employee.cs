﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SDK.EntityRepository.Entities;

namespace ClinicControlCenter.Domain.Models
{
    public class Employee : Entity
    {
        public long PersonId { get; set; }

        public virtual Person Person { get; set; }

        public DateTimeOffset ContractDate { get; set; }

        public decimal Salary { get; set; }

    }
}
