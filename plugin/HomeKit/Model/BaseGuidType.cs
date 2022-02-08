using System;

namespace HomeKit.Model
{
    public record BaseGuidType
    {
        private const string BaseGuid = "-0000-1000-8000-0026BB765291";
        public BaseGuidType(string accessoryValue)
        {
            this.Id = MakeFullGuid(accessoryValue);
        }

        public BaseGuidType(Guid id)
        {
            this.Id = id;
        }

        private static Guid MakeFullGuid(string value)
        {
            if (value.Length <= 8)
            {
                var prefix = new string('0', (8 - value.Length));
                return Guid.Parse($"{prefix}{value}{BaseGuid}");
            }

            // try all formats
            if (Guid.TryParse(value, out var result))
            {
                return result;
            }
            throw new ArgumentException("Invalid value: " + value);
        }

        public Guid Id { get; init; }
    }
}