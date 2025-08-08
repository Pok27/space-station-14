mech-verb-enter = Enter
mech-verb-exit = Remove pilot

mech-equipment-begin-install = Installing the {THE($item)}...
mech-equipment-finish-install = Finished installing the {THE($item)}

mech-equipment-select-popup = {$item} selected
mech-equipment-select-none-popup = Nothing selected

mech-radial-no-equipment = No Equipment

mech-ui-open-verb = Open control panel

mech-menu-title = mech control panel

mech-integrity-display = Integrity: {$amount}%
mech-energy-display = Energy: {$amount}%
mech-energy-missing = Energy: MISSING
mech-equipment-slot-display = Equipment: {$used}/{$max} used
mech-equipment-label = Equipment
mech-modules-label = Modules
mech-module-slot-display = Modules: {$used}/{$max} used

mech-air-cabin-label = Cabin air:
mech-air-toggle = Toggle Cabin

mech-fan-label = Fan:
mech-fan-on = On
mech-fan-off = Off
mech-fan-toggle = Toggle Fan
mech-fan-status = Status: { $state ->
    [idle] Idle
    [on] On
    *[off] Off
}
mech-fan-missing = No fan module installed
mech-cabin-gas-label = Cabin Pressure:
mech-cabin-gas-level = { $state ->
    [ok] {$level} kPa
    *[na] N/A
}

mech-no-enter = You cannot pilot this.

mech-eject-pilot-alert = {$user} is pulling the pilot out of the {$item}!

# Constraints
mech-install-blocked-pilot = Cannot install while a pilot is inside!
mech-remove-blocked-pilot = Cannot remove while a pilot is inside!
mech-capacity-equipment-full = No free equipment slots.
mech-capacity-modules-full = No free module capacity.
mech-duplicate-equipment = Identical equipment already installed.
mech-duplicate-module = Identical module already installed.

# Lock system
mech-lock-dna-label = DNA Lock:
mech-lock-card-label = ID Lock:
mech-lock-register = Register Lock
mech-lock-activate = Activate
mech-lock-deactivate = Deactivate
mech-lock-reset = Reset
mech-lock-no-dna = You don't have DNA to lock with!
mech-lock-no-card = You don't have an ID card to lock with!
mech-lock-dna-registered = DNA lock registered!
mech-lock-card-registered = ID lock registered!
mech-lock-activated = Lock activated!
mech-lock-deactivated = Lock deactivated!
mech-lock-reset-success = Lock reset!
mech-lock-access-denied = Access denied! This mech is locked.
mech-lock-dna-info = DNA: {$dna}
mech-lock-card-info = ID: {$name}
mech-lock-not-set = Not set
