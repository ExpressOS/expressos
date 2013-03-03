;; dafny-mode.el --- major mode for editing Dafny program files

;; Author: Haohui Mai <mai4@illinois.edu>


;;; -------------------------------------------------------------------------
;;; Code:

;; -------------------------------------------------------------------------
;; The following constant values can be modified by the user in a .emacs file

(defconst dafny-block-indent 2
  "*Controls indentation of lines within a block (`{') construct")

(defconst dafny-selection-indent 2
  "*Controls indentation of options within a selection (`if')
or iteration (`do') construct")

(defconst dafny-selection-option-indent 3
  "*Controls indentation of lines after options within selection or
iteration construct (`::')")

(defconst dafny-comment-col 32
  "*Defines the desired comment column for comments to the right of text.")

(defconst dafny-tab-always-indent t
  "*Non-nil means TAB in Dafny mode should always reindent the current line,
regardless of where in the line point is when the TAB command is used.")

(defconst dafny-auto-match-delimiter t
  "*Non-nil means typing an open-delimiter (i.e. parentheses, brace, quote, etc)
should also insert the matching closing delmiter character.")

;; That should be about it for most users...
;; unless you wanna hack elisp, the rest of this is probably uninteresting

;; -------------------------------------------------------------------------
;; dafny-mode font faces/definitions

;; make use of font-lock stuff, so say that explicitly
(require 'font-lock)

;; BLECH!  YUCK!   I just wish these guys could agree to something....
;; Faces available in:         ntemacs emacs  xemacs xemacs xemacs    
;;     font-lock- xxx -face     20.6   19.34  19.16   20.x   21.x
;;       -builtin-                X                             
;;       -constant-               X                             
;;       -comment-                X      X      X      X      X
;;       -doc-string-                           X      X      X
;;       -function-name-          X      X      X      X      X
;;       -keyword-                X      X      X      X      X
;;       -preprocessor-                         X      X      X
;;       -reference-                     X      X      X      X
;;       -signal-name-                          X      X!20.0 
;;       -string-                 X      X      X      X      X
;;       -type-                   X      X      X      X      X
;;       -variable-name-          X      X      X      X      X
;;       -warning-                X                           X

;;; Compatibility on faces between versions of emacs-en

;; send-poll "symbol" face is custom to dafny-mode 
;; but check for existence to allow someone to override it
(defvar dafny-fl-send-poll-face 'dafny-fl-send-poll-face
  "Face name to use for Dafny Send or Poll symbols: `!' or `?'")
(copy-face 'region 'dafny-fl-send-poll-face)

;; some emacs-en don't define or have regexp-opt available.  
(unless (functionp 'regexp-opt)
  (defmacro regexp-opt (strings)
    "Cheap imitation of `regexp-opt' since it's not availble in this emacs"
    `(mapconcat 'identity ,strings "\\|")))
  

;; -------------------------------------------------------------------------
;; dafny-mode font lock specifications/regular-expressions
;;   - for help, look at definition of variable 'font-lock-keywords
;;   - some fontification ideas from -- [engstrom:20010309.1435CST]
;; Pat Tullman (tullmann@cs.utah.edu) and
;; Ny Aina Razermera Mamy (ainarazr@cs.uoregon.edu)
;;     both had dafny-mode's that I discovered after starting this one...
;;     (but neither did any sort of indentation ;-)

(defconst dafny-font-lock-keywords-1 nil
  "Subdued level highlighting for Dafny mode.")

(defconst dafny-font-lock-keywords-2 nil
  "Medium level highlighting for Dafny mode.")

(defconst dafny-font-lock-keywords-3 nil
  "Gaudy level highlighting for Dafny mode.")

;; set each of those three variables now..
(let ((dafny-keywords
       (eval-when-compile 
         (regexp-opt 
          '("module" "imports" "class" "method" "extern" "static" "function"
            "returns" "return" "if" "then" "else" "var" "while" "private" "public" "internal" "ghost" "invariant" "reads"
	    "requires" "modifies" "ensures" "decreases" "new" "readonly"
            "case" "match"))))
      (dafny-types
       (eval-when-compile
         (regexp-opt '("int" "bool" "nat" "byte" "int32"
                       )))))

  ;; really simple fontification (strings and comments come for "free")
  (setq dafny-font-lock-keywords-1
    (list
     ;; Keywords:
     (cons (concat "\\<\\(" dafny-keywords "\\)\\>")
           'font-lock-keyword-face)
     ;; Types:
     (cons (concat "\\<\\(" dafny-types "\\)\\>")
           'font-lock-type-face)
     ;; Comments
     '("\\(//.*\\)"1 'font-lock-comment-face t)
     ))

  ;; more complex fontification
  ;; add function (proctype) names, lables and goto statements
  ;; also add send/receive/poll fontification
  (setq dafny-font-lock-keywords-2
   (append dafny-font-lock-keywords-1
    (list
     ;; ANY Pre-Processor directive (lazy method: any line beginning with "#[a-z]+")
     '("^\\(#[ \t]*[a-z]+\\)"1 'font-lock-preprocessor-face t)
    )))

  ;; most complex fontification
  ;; add pre-processor directives, typed variables and hidden/typedef decls.
  (setq dafny-font-lock-keywords-3
   (append dafny-font-lock-keywords-2
    (list
     ;; ANY Pre-Processor directive (lazy method: any line beginning with "#[a-z]+")
     ;;'("^\\(#[ \t]*[a-z]+\\)"1 'font-lock-preprocessor-face t)
     ;; "defined" in an #if or #elif and associated macro names
     '("^#[ \t]*\\(el\\)?if\\>"
       ("\\<\\(defined\\)\\>[ \t]*(?\\(\\sw+\\)" nil nil
        (1 'font-lock-preprocessor-face nil t)
        (2 'font-lock-reference-face nil t)))
     '("^#[ \t]*ifn?def\\>"
       ("[ \t]*\\(\\sw+\\)" nil nil
        (1 'font-lock-reference-face nil t)))
     ;; Filenames in #include <...> directives
     '("^#[ \t]*include[ \t]+<\\([^>\"\n]+\\)>"1 'font-lock-string-face nil t)
     ;; Defined functions and constants/types (non-functions)
     '("^#[ \t]*define[ \t]+"
       ("\\(\\sw+\\)(" nil nil (1 'font-lock-function-name-face nil t))
       ("\\(\\sw+\\)[ \t]+\\(\\sw+\\)"nil nil (1 'font-lock-variable-name-face)
	(2 'font-lock-reference-face nil t))
       ("\\(\\sw+\\)[^(]?"nil nil (1 'font-lock-reference-face nil t)))

     ;; Types AND variables
     ;;   - room for improvement: (i.e. don't currently):
     ;;     highlight user-defined types and asociated variable declarations
     (list (concat "\\<\\(" dafny-types "\\)\\>")
          ;;'(1 'font-lock-type-face)
          ;; now match the variables after the type definition, if any
          '(dafny-match-variable-or-declaration
            nil nil
            (1 'font-lock-variable-name-face) ;; nil t)
            (2 font-lock-reference-face nil t)))
     )))
  )

(defvar dafny-font-lock-keywords dafny-font-lock-keywords-1
  "Default expressions to highlight in Dafny mode.")

;; Font-lock matcher functions:
(defun dafny-match-variable-or-declaration (limit)
  "Match, and move over, any declaration/definition item after point.
Matches after point, but ignores leading whitespace characters.
Does not move further than LIMIT.

The expected syntax of a declaration/definition item is `word' (preceded
by optional whitespace) optionally followed by a `= value' (preceded and
followed by more optional whitespace)

Thus the regexp matches after point:word [ = value ]
^^^^     ^^^^^
Where the match subexpressions are:  1        2

The item is delimited by (match-beginning 1) and (match-end 1).
If (match-beginning 2) is non-nil, the item is followed by a `value'."
  (when (looking-at "[ \t]*\\(\\sw+\\)[ \t]*=?[ \t]*\\(\\sw+\\)?[ \t]*,?")
    (goto-char (min limit (match-end 0)))))


;; -------------------------------------------------------------------------
;; "install" dafny-mode font lock specifications

;; FMI: look up 'font-lock-defaults
(defconst dafny-font-lock-defaults
  '(
    (dafny-font-lock-keywords 
     dafny-font-lock-keywords-1
     dafny-font-lock-keywords-2
     dafny-font-lock-keywords-3)  ;; font-lock stuff (keywords)
    nil  ;; keywords-only flag
    nil  ;; case-fold keyword searching
    ;;((?_ . "w") (?$ . "."))  ;; mods to syntax table
    nil  ;; mods to syntax table (see below)
    nil  ;; syntax-begin
    (font-lock-mark-block-function . mark-defun))
)

;; "install" the font-lock-defaults based upon version of emacs we have
(not (assq 'dafny-mode font-lock-defaults-alist))
 (setq font-lock-defaults-alist
       (cons
	(cons 'dafny-mode dafny-font-lock-defaults)
	font-lock-defaults-alist))


;; -------------------------------------------------------------------------
;; other dafny-mode specific definitions

(defconst dafny-defun-prompt-regexp
  "^[ \t]*\\(d?proctype\\|init\\|inline\\|never\\|trace\\|typedef\\|mtype\\s-+=\\)[^{]*"
  "Regexp describing the beginning of a Dafny top-level definition.")

(defvar dafny-mode-abbrev-table nil
  "*Abbrev table in use in dafny-mode buffers.")
(if dafny-mode-abbrev-table
    nil
  (define-abbrev-table 'dafny-mode-abbrev-table
    '(
;; Commented out for now - need to think about what abbrevs make sense
      ;;     ("assert" "ASSERT" dafny-check-expansion 0)
      ;;     ("d_step""D_STEP"dafny-check-expansion 0)
      ;;     ("break" "BREAK" dafny-check-expansion 0)
      ;;     ("do" "DO" dafny-check-expansion 0)
      ;;     ("proctype""PROCTYPE" dafny-check-expansion 0)
      )))

(defvar dafny-mode-map nil
  "Keymap for dafny-mode.")
(if dafny-mode-map
    nil
  (setq dafny-mode-map (make-sparse-keymap))
  (define-key dafny-mode-map "\t"'dafny-indent-command)
  (define-key dafny-mode-map "\C-m"'dafny-newline-and-indent)
					;(define-key dafny-mode-map 'backspace'backward-delete-char-untabify)
  (define-key dafny-mode-map "\C-c\C-p"'dafny-beginning-of-block)
					;(define-key dafny-mode-map "\C-c\C-n"'dafny-end-of-block)
  (define-key dafny-mode-map "\M-\C-a"'dafny-beginning-of-defun)
					;(define-key dafny-mode-map "\M-\C-e"'dafny-end-of-defun)
  (define-key dafny-mode-map "\C-c("'dafny-toggle-auto-match-delimiter)
  (define-key dafny-mode-map "{"'dafny-open-delimiter)
  (define-key dafny-mode-map "}" 'dafny-close-delimiter)
  (define-key dafny-mode-map "("'dafny-open-delimiter)
  (define-key dafny-mode-map ")" 'dafny-close-delimiter)
  (define-key dafny-mode-map "["'dafny-open-delimiter)
  (define-key dafny-mode-map "]" 'dafny-close-delimiter)
  (define-key dafny-mode-map ";"'dafny-insert-and-indent)
  (define-key dafny-mode-map ":"'dafny-insert-and-indent)
  ;;
  ;;(define-key dafny-mode-map "\C-c\C-d"'dafny-mode-toggle-debug)
  ;;(define-key dafny-mode-map "\C-c\C-r"'dafny-mode-revert-buffer)
  )

(defvar dafny-matching-delimiter-alist
  '( (?(  . ?))
     (?[  . ?])
     (?{  . "\n}")
     ;(?<  . ?>)
     (?\' . ?\')
     (?\` . ?\`)
     (?\" . ?\") )
  "List of pairs of matching open/close delimiters - for auto-insert")


;; -------------------------------------------------------------------------
;; Dafny-mode itself

(defun dafny-mode ()
  "Major mode for editing DAFNY code.
\\{dafny-mode-map}

Variables controlling indentation style:
  dafny-block-indent
Relative offset of lines within a block (`{') construct.

  dafny-selection-indent
  Relative offset of option lines within a selection (`if')
or iteration (`do') construct.

  dafny-selection-option-indent
Relative offset of lines after/within options (`::') within
 selection or iteration constructs.

  dafny-comment-col
Defines the desired comment column for comments to the right of text.

  dafny-tab-always-indent
Non-nil means TAB in DAFNY mode should always reindent the current
line, regardless of where in the line the point is when the TAB
command is used.

  dafny-auto-match-delimiter
Non-nil means typing an open-delimiter (i.e. parentheses, brace,
        quote, etc) should also insert the matching closing delmiter
        character.

Turning on DAFNY mode calls the value of the variable dafny-mode-hook with
no args, if that value is non-nil.

For example: '
(setq dafny-mode-hook '(lambda ()
			   (setq dafny-block-indent 2)
			   (setq dafny-selection-indent 0)
			   (setq dafny-selection-option-indent 2)
			   (local-set-key \"\\C-m\" 'dafny-indent-newline-indent)
			   ))'

will indent block two steps, will make selection options aligned with DO/IF
and sub-option lines indent to a column after the `::'.  Also, lines will
be reindented when you hit RETURN.

Note that dafny-mode adhears to the font-lock \"standards\" and
defines several \"levels\" of fontification or colorization.  The
default is fairly gaudy, so if you would prefer a bit less, please see
the documentation for the variable: `font-lock-maximum-decoration'.
"
  (interactive)
  (kill-all-local-variables)
  (setq mode-name  "Dafny")
  (setq major-mode 'dafny-mode)
  (use-local-map dafny-mode-map)
  (setq local-abbrev-table dafny-mode-abbrev-table)

  ;; Make local variables
  (make-local-variable 'case-fold-search)
  (make-local-variable 'paragraph-start)
  (make-local-variable 'paragraph-separate)
  (make-local-variable 'paragraph-ignore-fill-prefix)
  (make-local-variable 'indent-line-function)
  (make-local-variable 'indent-region-function)
  (make-local-variable 'parse-sexp-ignore-comments)
  (make-local-variable 'comment-start)
  (make-local-variable 'comment-end)
  (make-local-variable 'comment-column)
  (make-local-variable 'comment-start-skip)
  (make-local-variable 'comment-indent-hook)
  (make-local-variable 'defun-prompt-regexp)
  (make-local-variable 'compile-command)
  ;; Now set their values
  (setq case-fold-search t
        paragraph-start (concat "^$\\|" page-delimiter)
        paragraph-separate paragraph-start
        paragraph-ignore-fill-prefix t
        indent-line-function 'dafny-indent-command
;;indent-region-function 'dafny-indent-region
        parse-sexp-ignore-comments t
        comment-start "/* "
        comment-end " */"
        comment-column 32
        comment-start-skip "/\\*+ *"
;;        comment-start-skip "/\\*+ *\\|// *"
        ;;comment-indent-hook 'dafny-comment-indent
        defun-prompt-regexp dafny-defun-prompt-regexp
        )

  ;; Turn on font-lock mode
  ;; (and dafny-font-lock-mode (font-lock-mode))
  (font-lock-mode)

  ;; Finally, run the hooks and be done.
  (run-hooks 'dafny-mode-hook))


;; -------------------------------------------------------------------------
;; Interactive functions
;;

(defun dafny-mode-version ()
  "Print the current version of dafny-mode in the minibuffer"
  (interactive)
  (message (concat "Dafny-Mode: " dafny-mode-version)))

(defun dafny-beginning-of-block ()
  "Move backward to start of containing block.
Containing block may be `{', `do' or `if' construct, or comment."
  (interactive)
  (goto-char (dafny-find-start-of-containing-block-or-comment)))

(defun dafny-beginning-of-defun (&optional arg)
  "Move backward to the beginning of a defun.
With argument, do it that many times.
Negative arg -N means move forward to Nth following beginning of defun.
Returns t unless search stops due to beginning or end of buffer.

See also 'beginning-of-defun.

This is a Dafny-mode specific version since default (in xemacs 19.16 and
NT-Emacs 20) don't seem to skip comments - they will stop inside them.

Also, this makes sure that the beginning of the defun is actually the
line which starts the proctype/init/etc., not just the open-brace."
  (interactive "p")
  (beginning-of-defun arg)
  (if (not (looking-at dafny-defun-prompt-regexp))
      (re-search-backward dafny-defun-prompt-regexp nil t))
  (if (dafny-inside-comment-p)
      (goto-char (dafny-find-start-of-containing-comment))))

(defun dafny-indent-command ()
  "Indent the current line as DAFNY code."
  (interactive)
  (if (and (not dafny-tab-always-indent)
           (save-excursion
             (skip-chars-backward " \t")
             (not (bolp))))
      (tab-to-tab-stop)
    (dafny-indent-line)))

(defun dafny-newline-and-indent ()
  "Dafny-mode specific newline-and-indent which expands abbrevs before
running a regular newline-and-indent."
  (interactive)
  (if abbrev-mode
      (expand-abbrev))
  (newline-and-indent))

(defun dafny-indent-newline-indent ()
  "Dafny-mode specific newline-and-indent which expands abbrevs and
indents the current line before running a regular newline-and-indent."
  (interactive)
  (save-excursion (dafny-indent-command))
  (if abbrev-mode
      (expand-abbrev))
  (newline-and-indent))

(defun dafny-insert-and-indent ()
  "Insert the last character typed and re-indent the current line"
  (interactive)
  (insert last-command-char)
  (save-excursion (dafny-indent-command)))

(defun dafny-open-delimiter ()
  "Inserts the open and matching close delimiters, indenting as appropriate."
  (interactive)
  (insert last-command-char)
  (if (and dafny-auto-match-delimiter (not (dafny-inside-comment-p)))
      (save-excursion
        (insert (cdr (assq last-command-char dafny-matching-delimiter-alist)))
        (dafny-indent-command))))

(defun dafny-close-delimiter ()
  "Inserts and indents a close delimiter."
  (interactive)
  (insert last-command-char)
  (if (not (dafny-inside-comment-p))
      (save-excursion (dafny-indent-command))))

(defun dafny-toggle-auto-match-delimiter ()
  "Toggle auto-insertion of parens and other delimiters.
See variable `dafny-auto-insert-matching-delimiter'"
  (interactive)
  (setq dafny-auto-match-delimiter
        (not dafny-auto-match-delimiter))
  (message (concat "Dafny auto-insert matching delimiters "
                   (if dafny-auto-match-delimiter
                       "enabled" "disabled"))))


;; -------------------------------------------------------------------------
;; Compilation/Verification functions

;; all of this is in serious "beta" mode - don't trust it ;-)
;; (setq 
;;  dafny-compile-command"spin "
;;  dafny-syntax-check-args"-a -v "
;; )

;;(setq compilation-error-regexp-alist
;;      (append compilation-error-regexp-alist
;;              '(("spin: +line +\\([0-9]+\\) +\"\\([^\"]+\\)\"" 2 1))))

;; (defun dafny-syntax-check ()
;;   (interactive)
;;   (compile (concat dafny-compile-command
;;                    dafny-syntax-check-args
;;                    (buffer-name))))


;; -------------------------------------------------------------------------
;; Indentation support functions

;; Note that indentation is based ENTIRELY upon the indentation of the
;; previous line(s), esp. the previous non-blank line and the line
;; starting the current containgng block...
(defun dafny-indent-line ()
  "Indent the current line as DAFNY code.
Return the amount the by which the indentation changed."
  (beginning-of-line)
  (let ((indent (dafny-calc-indent))
	beg
	shift-amt
	(pos (- (point-max) (point))))
    (setq beg (point))
    (skip-chars-forward " \t")
    (setq shift-amt (- indent (current-column)))
    (if (zerop shift-amt)
	(if (> (- (point-max) pos) (point))
	    (goto-char (- (point-max) pos)))
      (delete-region beg (point))
      (indent-to indent)
      (if (> (- (point-max) pos) (point))
	  (goto-char (- (point-max) pos))))
    shift-amt))

(defun dafny-calc-indent ()
  "Return the appropriate indentation for this line as an int."
  (save-excursion
    (beginning-of-line)
    (let* ((orig-point  (point))
           (state       (dafny-parse-partial-sexp))
           (paren-depth (nth 0 state))
           (paren-point (or (nth 1 state) 1))
           (paren-char  (char-after paren-point)))
      ;;(what-cursor-position)
      (cond
       ;; Indent not-at-all - inside a string
       ((nth 3 state)
        (current-indentation))
       ;; Indent inside a comment
       ((nth 4 state)
        (dafny-calc-indent-within-comment))
       ;; looking at a pre-processor directive - indent=0
       ((looking-at "[ \t]*#\\(define\\|if\\(n?def\\)?\\|else\\|endif\\)")
        0)
       ;; If we're not inside a "true" block (i.e. "{}"), then indent=0
       ;; I think this is fair, since no (indentable) code in dafny
       ;; exists outside of a proctype or similar "{ .. }" structure.
       ((zerop paren-depth)
        0)
       ;; Indent relative to non curly-brace "paren"
       ;; [ NOTE: I'm saving this, but don't use it any more.
       ;;         Now, we let parens be indented like curly braces
       ;;((and (>= paren-depth 1) (not (char-equal ?\{ paren-char)))
       ;; (goto-char paren-point)
       ;; (1+ (current-column)))
       ;; 
       ;; Last option: indent relative to contaning block(s)
       (t
        (goto-char orig-point)
        (dafny-calc-indent-within-block paren-point))))))

(defun dafny-calc-indent-within-block (&optional limit)
  "Return the appropriate indentation for this line, assume within block.
with optional arg, limit search back to `limit'"
  (save-excursion
    (let* ((stop  (or limit 1))
           (block-point  (dafny-find-start-of-containing-block stop))
           (block-type   (dafny-block-type-after block-point))
           (indent-point (point))
           (indent-type  (dafny-block-type-after indent-point)))
      (if (not block-type) 0
        ;;(message "paren: %d (%d); block: %s (%d); indent: %s (%d); stop: %d"
        ;;         paren-depth paren-point block-type block-point
        ;;         indent-type indent-point stop)
        (goto-char block-point)
        (cond
         ;; indent code inside "{"
         ((eq 'block block-type)
          (cond
           ;; if we are indenting the end of a block,
           ;; use indentation of start-of-block
           ((equal 'block-end indent-type)
            (current-indentation))
           ;; if the start of the code inside the block is not at eol
           ;; then indent to the same column as the block start +some
           ;; [ but ignore comments after "{" ]
           ((and (not (dafny-effective-eolp (1+ (point))))
                 (not (looking-at "{[ \t]*/\\*")))
            (forward-char); skip block-start
            (skip-chars-forward "{ \t") ; skip whitespace, if any
            (current-column))
           ;; anything else we indent +dafny-block-indent from
           ;; the indentation of the start of block (where we are now)
           (t
            (+ (current-indentation)
               dafny-block-indent))))
         ;; dunno what kind of block this is - sound an error
         (t
          (error "dafny-calc-indent-within-block: unknown block type: %s" block-type)
          (current-indentation)))))))

(defun dafny-calc-indent-within-comment ()
  "Return the indentation amount for line, assuming that the
current line is to be regarded as part of a block comment."
  (save-excursion
    (beginning-of-line)
    (skip-chars-forward " \t")
    (let ((indenting-end-of-comment (looking-at "\\*/"))
          (indenting-blank-line (eolp)))
      ;; if line is NOT blank and next char is NOT a "*'
      (if (not (or indenting-blank-line (= (following-char) ?\*)))
          ;; leave indent alone
          (current-column)
        ;; otherwise look back for _PREVIOUS_ possible nested comment start
        (let ((comment-start (save-excursion 
                               (re-search-backward comment-start-skip))))
          ;; and see if there is an appropriate middle-comment "*"
          (if (re-search-backward "^[ \t]+\\*" comment-start t)
              (current-indentation)
            ;; guess not, so indent relative to comment start
            (goto-char comment-start)
            (if indenting-end-of-comment
                (current-column)
              (1+ (current-column)))))))))


;; -------------------------------------------------------------------------
;; Misc other support functions

(defun dafny-parse-partial-sexp (&optional start limit)
  "Return the partial parse state of current defun or from optional start
to end limit"
  (save-excursion
    (let ((end (or limit (point))))
      (if start
          (goto-char start)
        (dafny-beginning-of-defun))
      (parse-partial-sexp (point) end))))

;;(defun dafny-at-end-of-block-p ()
;;  "Return t if cursor is at the end of a dafny block"
;;  (save-excursion
;;    (let ((eol (progn (end-of-line) (point))))
;;      (beginning-of-line)
;;      (skip-chars-forward " \t")
;;      ;;(re-search-forward "\\(}\\|\\b\\(od\\|fi\\)\\b\\)" eol t))))
;;      (looking-at "[ \t]*\\(od\\|fi\\)\\b"))))

(defun dafny-inside-comment-p ()
  "Check if the point is inside a comment block."
  (save-excursion
    (let ((origpoint (point))
          state)
      (goto-char 1)
      (while (> origpoint (point))
	(setq state (parse-partial-sexp (point) origpoint 0)))
      (nth 4 state))))

(defun dafny-inside-comment-or-string-p ()
  "Check if the point is inside a comment or a string."
  (save-excursion
    (let ((origpoint (point))
          state)
      (goto-char 1)
      (while (> origpoint (point))
	(setq state (parse-partial-sexp (point) origpoint 0)))
      (or (nth 3 state) (nth 4 state)))))


(defun dafny-effective-eolp (&optional point)
  "Check if we are at the effective end-of-line, ignoring whitespace"
  (save-excursion
    (if point (goto-char point))
    (skip-chars-forward " \t")
    (eolp)))

(defun dafny-check-expansion ()
  "If abbrev was made within a comment or a string, de-abbrev!"
  (if dafny-inside-comment-or-string-p
      (unexpand-abbrev)))

(defun dafny-block-type-after (&optional point)
  "Return the type of block after current point or parameter as a symbol.
Return 'block `{' or `}' or nil if none of the above match."
  (save-excursion
    (goto-char (or point (point)))
    (skip-chars-forward " \t")
    (cond
     ((looking-at "[{(]") 'block)
     ((looking-at "[})]") 'block-end)
     (t nil))))

(defun dafny-find-start-of-containing-comment (&optional limit)
  "Return the start point of the containing comment block.
Stop at `limit' or beginning of buffer."
  (let ((stop (or limit 1)))
    (save-excursion
      (while (and (>= (point) stop)
                  (nth 4 (dafny-parse-partial-sexp)))
        (re-search-backward comment-start-skip stop t))
      (point))))

(defun dafny-find-start-of-containing-block (&optional limit)
  "Return the start point of the containing `do', `if', `::' or
`{' block or containing comment.
Stop at `limit' or beginning of buffer."
  (save-excursion
    (skip-chars-forward " \t")
    (let* ((type  (dafny-block-type-after))
           (stop  (or limit
                      (save-excursion (dafny-beginning-of-defun) (point))))
           (state (dafny-parse-partial-sexp stop))
           (level (if (looking-at "\\(od\\)\\b")
                      2
                    (if (zerop (nth 0 state)) 0 1))))
      ;;(message "find-start-of-containing-block: type: %s; level %d; stop %d"
      ;;         type level stop)
      (while (and (> (point) stop) (not (zerop level)))
	(re-search-backward
             "\\({\\|}\\)"
             stop 'move)
        ;;(message "looking from %d back-to %d" (point) stop)
	(setq state (dafny-parse-partial-sexp stop))
	(setq level (+ level
                       (cond ((or (nth 3 state) (nth 4 state))     0)
                             ((and (= 1 level) (looking-at "::")
                                   (not (equal type 'option)))  -1)
                             ((looking-at "{") -1)
                             ((looking-at "}") +1)
                             (t 0)))))
      (point))))

(defun dafny-find-start-of-containing-block-or-comment (&optional limit)
  "Return the start point of the containing comment or
the start of the containing `do', `if', `::' or `{' block.
Stop at limit or beginning of buffer."
  (if (dafny-inside-comment-p)
      (dafny-find-start-of-containing-comment limit)
    (dafny-find-start-of-containing-block limit)))

;; -------------------------------------------------------------------------
;; Debugging/testing

;; (defun dafny-mode-toggle-debug ()
;;   (interactive)
;;   (make-local-variable 'debug-on-error)
;;   (setq debug-on-error (not debug-on-error)))

;;(defun dafny-mode-revert-buffer ()
;;  (interactive)
;;  (revert-buffer t t))

;; -------------------------------------------------------------------------
;;###autoload

(provide 'dafny-mode)