using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LabSync.Core.Entities
{
    public class DeviceGroup
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Navigation property to devices assigned to this group.
        /// </summary>
        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}