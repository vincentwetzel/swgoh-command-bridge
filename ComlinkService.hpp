#pragma once

#include <string>
#include <future>

namespace swgoh_command_bridge {

class ComlinkService {
public:
    // Constructor accepting the local swgoh-comlink base URL (e.g., "http://localhost:3000")
    explicit ComlinkService(std::string baseUrl);
    virtual ~ComlinkService() = default;

    // Fetches raw player profile, character and mod data using ally code
    virtual std::future<std::string> fetchPlayerRaw(const std::string& allyCode);

    // Fetches game metadata
    virtual std::future<std::string> fetchMetaDataRaw();

private:
    std::string m_baseUrl;
};

} // namespace swgoh_command_bridge