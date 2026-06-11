#include "ComlinkService.hpp"
#include <httplib.h>
#include <nlohmann/json.hpp>
#include <stdexcept>

namespace swgoh_command_bridge {

ComlinkService::ComlinkService(std::string baseUrl) 
    : m_baseUrl(std::move(baseUrl)) {}

std::future<std::string> ComlinkService::fetchPlayerRaw(const std::string& allyCode) {
    return std::async(std::launch::async, [this, allyCode]() {
        httplib::Client cli(m_baseUrl);
        
        nlohmann::json body = {
            {"payload", {{"allyCode", allyCode}}},
            {"enums", true}
        };

        if (auto res = cli.Post("/player", body.dump(), "application/json")) {
            if (res->status == 200) {
                return res->body;
            }
            throw std::runtime_error("Comlink response error: " + std::to_string(res->status));
        }
        throw std::runtime_error("Failed to connect to swgoh-comlink at " + m_baseUrl);
    });
}

std::future<std::string> ComlinkService::fetchMetaDataRaw() {
    return std::async(std::launch::async, [this]() {
        httplib::Client cli(m_baseUrl);
        if (auto res = cli.Get("/metadata")) {
            return res->body;
        }
        throw std::runtime_error("Failed to fetch metadata from " + m_baseUrl);
    });
}

} // namespace swgoh_command_bridge