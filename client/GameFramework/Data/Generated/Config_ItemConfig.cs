using System.Collections.Generic;

namespace GameFramework.Data.Generated
{
    /// <summary>
    /// 自动生成的配置行类 - ItemConfig
    /// </summary>
    public class Config_ItemConfig : IConfigRow
    {
        /// <summary>
        /// 唯一ID
        /// </summary>
        [FieldIndex(0)]
        public int _id { get; set; }
        /// <summary>
        /// 物品名称
        /// </summary>
        [FieldIndex(1)]
        public string Name { get; set; }
        /// <summary>
        /// 类型ID
        /// </summary>
        [FieldIndex(2)]
        public int Type { get; set; }
        /// <summary>
        /// 品质等级(1-5)
        /// </summary>
        [FieldIndex(3)]
        public int Quality { get; set; }
        /// <summary>
        /// 最大堆叠
        /// </summary>
        [FieldIndex(4)]
        public int MaxStack { get; set; }
        /// <summary>
        /// 使用类型(0=无 1=消耗)
        /// </summary>
        [FieldIndex(5)]
        public int UseType { get; set; }
        /// <summary>
        /// 描述
        /// </summary>
        [FieldIndex(6)]
        public string Description { get; set; }

        public int Id => _id;
    }
}