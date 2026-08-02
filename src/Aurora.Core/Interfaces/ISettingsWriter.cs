using Aurora.Core.Settings;

namespace Aurora.Core.Interfaces;

public interface ISettingsWriter
{
    void Save(SettingsData data);
}
