+++
title = "Sonic Eddy"
+++

<section class="flow">
  <div class="flow-meta">Dual&#8209;layer digital performance mixer &middot; 16 stereo channels / 8 groups / 1 main</div>
  <div class="flow-stats">
    <div class="flow-stat"><span class="flow-stat-num">16</span><span class="flow-stat-label">Channels</span></div>
    <div class="flow-stat"><span class="flow-stat-num">2</span><span class="flow-stat-label">Mirrored Layers</span></div>
    <div class="flow-stat"><span class="flow-stat-num">8</span><span class="flow-stat-label">Group Buses</span></div>
    <div class="flow-stat"><span class="flow-stat-num">6</span><span class="flow-stat-label">FX Returns</span></div>
  </div>
  <div class="flow-layers">
    <div class="flow-layer flow-layer-a">
      <div class="flow-layer-head">
        <span class="flow-layer-title">Layer A</span>
        <span class="flow-badge">Fully Independent</span>
      </div>
      <div class="flow-sub">8 Channels</div>
      <div class="flow-grid flow-grid-8">
        <div class="flow-cell">01</div><div class="flow-cell">02</div><div class="flow-cell">03</div><div class="flow-cell">04</div>
        <div class="flow-cell">05</div><div class="flow-cell">06</div><div class="flow-cell">07</div><div class="flow-cell">08</div>
      </div>
      <div class="flow-connector"></div>
      <div class="flow-sub">4 Group Buses &middot; Assign Any Channel</div>
      <div class="flow-grid flow-grid-4">
        <div class="flow-pill">G1</div><div class="flow-pill">G2</div><div class="flow-pill">G3</div><div class="flow-pill">G4</div>
      </div>
      <div class="flow-connector"></div>
      <div class="flow-sub">Layer FX &middot; 2 Sends, 2 Returns</div>
      <div class="flow-grid flow-grid-2">
        <div class="flow-fx"><div class="flow-fx-title">Send 1 &rarr; Ret 1</div><div class="flow-fx-desc">Filter chains, or external effects</div></div>
        <div class="flow-fx"><div class="flow-fx-title">Send 2 &rarr; Ret 2</div><div class="flow-fx-desc">Filter chains, or external effects</div></div>
      </div>
      <div class="flow-connector"></div>
      <div class="flow-master">Layer Master</div>
    </div>
    <div class="flow-layer flow-layer-b">
      <div class="flow-layer-head">
        <span class="flow-layer-title">Layer B</span>
        <span class="flow-badge">Identical, Mirrored</span>
      </div>
      <div class="flow-sub">8 Channels</div>
      <div class="flow-grid flow-grid-8">
        <div class="flow-cell">01</div><div class="flow-cell">02</div><div class="flow-cell">03</div><div class="flow-cell">04</div>
        <div class="flow-cell">05</div><div class="flow-cell">06</div><div class="flow-cell">07</div><div class="flow-cell">08</div>
      </div>
      <div class="flow-connector"></div>
      <div class="flow-sub">4 Group Buses &middot; Assign Any Channel</div>
      <div class="flow-grid flow-grid-4">
        <div class="flow-pill">G1</div><div class="flow-pill">G2</div><div class="flow-pill">G3</div><div class="flow-pill">G4</div>
      </div>
      <div class="flow-connector"></div>
      <div class="flow-sub">Layer FX &middot; 2 Sends, 2 Returns</div>
      <div class="flow-grid flow-grid-2">
        <div class="flow-fx"><div class="flow-fx-title">Send 1 &rarr; Ret 1</div><div class="flow-fx-desc">Filter chains, or external effects</div></div>
        <div class="flow-fx"><div class="flow-fx-title">Send 2 &rarr; Ret 2</div><div class="flow-fx-desc">Filter chains, or external effects</div></div>
      </div>
      <div class="flow-connector"></div>
      <div class="flow-master">Layer Master</div>
    </div>
  </div>
  <div class="flow-connector flow-connector-center"></div>
  <div class="flow-bus">
    <span class="flow-bus-label">Global Send Bus</span>
    <span class="flow-bus-desc">any channel or group, either layer</span>
  </div>
  <div class="flow-connector flow-connector-center"></div>
  <div class="flow-returns">
    <div class="flow-return"><div class="flow-return-title">Global Ret 1</div><div class="flow-return-desc">Filter chains, or external effects, shared by both layers</div></div>
    <div class="flow-return"><div class="flow-return-title">Global Ret 2</div><div class="flow-return-desc">Filter chains, or external effects, shared by both layers</div></div>
  </div>
  <div class="flow-connector flow-connector-center"></div>
  <div class="flow-globalmaster">
    <div class="flow-globalmaster-title">Global Master</div>
    <div class="flow-globalmaster-desc">both layer masters + both global returns</div>
  </div>
  <div class="flow-connector flow-connector-center"></div>
  <div class="flow-mainout">Main Out</div>
</section>
<hr class="flow-divider" />
<div class="hero-actions">
  <a href="/installation" class="button">Get Started</a>
  <a href="/features" class="button button-secondary">Features</a>
  <a href="/docs" class="button button-secondary">Documentation</a>
</div>
<img src="/logo.webp" alt="Sonic Eddy" class="hero-logo-inline" />

Sonic Eddy is a performance, and hardware-focused audio mixer for Linux, built
on top of PipeWire and WirePlumber.

It provides 16 stereo channels, and 8 group channels, both with 4 sends, and
optional plugins, arranged in 2 layers for live electronic music production.
Each channel provides a looper, which can record, and play, what the instrument
just played, in order to provide easy setup of a new melody, for example.

A vast box of hardware focused tools is onboard, from midi sync, click tracks,
Ableton link for synchronization, over virtual inputs and outputs, to, thanks to
the power of pipewire, mixing across multiple audio interfaces, including audio
interfaces provided by synthesizers like the Roland SE-02, is also on board.

## Development

Sonic Eddy is developed as open source project in the
[**Repository**](https://git.sr.ht/~frank6/sonic-eddy) on source hut.

## Stay in Touch

### Mailing list

<form
  action="https://buttondown.com/api/emails/embed-subscribe/sonic-eddy"
  method="post"
  class="embeddable-buttondown-form"
>
  <label for="bd-email">Enter your email</label>
  <input type="email" name="email" id="bd-email" />
  <input type="submit" value="Subscribe" />
  <p>
    <a href="https://buttondown.com/refer/sonic-eddy" target="_blank">
      Powered by Buttondown.
    </a>
  </p>
</form>

### Matrix channel

Join the discussion in our [**Matrix room**](https://matrix.to/#/#general:matrix.sonic-eddy.org).
