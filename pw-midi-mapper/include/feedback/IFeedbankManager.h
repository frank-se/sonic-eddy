#pragma once

#include <memory>

namespace feedback {

class IFeedbackManager {
public:
  virtual void initial_state() = 0;
  virtual void feedback_for_layer_id_change(size_t layer_id) = 0;

  virtual ~IFeedbackManager() = default;
};

using FeedbackManagerPtr = std::shared_ptr<IFeedbackManager>;

} // namespace feedback
